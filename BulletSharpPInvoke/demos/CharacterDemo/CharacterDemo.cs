﻿using BulletSharp;
using BulletSharp.Math;
using DemoFramework;
using System;
using System.Windows.Forms;

namespace CharacterDemo
{
    public class BspToBulletConverter : BspConverter
    {
        Demo demo;

        public BspToBulletConverter(Demo demo)
        {
            this.demo = demo;
        }

        public override void AddConvexVerticesCollider(AlignedVector3Array vertices, bool isEntity, Vector3 entityTargetLocation)
        {
            // perhaps we can do something special with entities (isEntity)
            // like adding a collision Triggering (as example)

            if (vertices.Count == 0)
                return;

            const float mass = 0.0f;
            //can use a shift
            Matrix startTransform = Matrix.Translation(0, -10.0f, 0);
            //this create an internal copy of the vertices
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i] * 0.5f;
                vertices[i] = new Vector3(v.X, v.Z * 0.75f, -v.Y);
            }

            CollisionShape shape = new ConvexHullShape(vertices);
            demo.CollisionShapes.Add(shape);

            demo.LocalCreateRigidBody(mass, startTransform, shape);
        }
    }

    class CharacterDemo : Demo
    {
        Vector3 eye = new Vector3(10, 0, 10);
        Vector3 target = new Vector3(0, 0, 0);

        PairCachingGhostObject ghostObject;
        KinematicCharacterController character;

        protected override void OnInitialize()
        {
            Freelook.Up = Vector3.UnitY;
            Freelook.SetEyeTarget(eye, target);

            Graphics.SetFormText("BulletSharp - Character Demo");
            Graphics.SetInfoText("Move using arrow keys\n" +
                "F3 - Toggle debug\n" +
                //"F11 - Toggle fullscreen\n" +
                "Space - Shoot box");
        }

        protected override void OnInitializePhysics()
        {
            // collision configuration contains default setup for memory, collision setup
            CollisionConf = new DefaultCollisionConfiguration();
            Dispatcher = new CollisionDispatcher(CollisionConf);

            Broadphase = new AxisSweep3(new Vector3(-1000, -1000, -1000), new Vector3(1000, 1000, 1000));
            Solver = new SequentialImpulseConstraintSolver();

            World = new DiscreteDynamicsWorld(Dispatcher, Broadphase, Solver, CollisionConf);
            World.DispatchInfo.AllowedCcdPenetration = 0.0001f;
            //World.Gravity = Freelook.Up * -10.0f;

            Matrix startTransform = Matrix.Translation(10.210098f, -1.6433364f, 16.453260f);
            ghostObject = new PairCachingGhostObject();
            ghostObject.WorldTransform = startTransform;
            Broadphase.OverlappingPairCache.SetInternalGhostPairCallback(new GhostPairCallback());

            const float characterHeight = 1.75f;
            const float characterWidth = 1.75f;
            ConvexShape capsule = new CapsuleShape(characterWidth, characterHeight);
            ghostObject.CollisionShape = capsule;
            ghostObject.CollisionFlags = CollisionFlags.CharacterObject;

            const float stepHeight = 0.35f;
            character = new KinematicCharacterController(ghostObject, capsule, stepHeight);

            BspLoader bspLoader = new BspLoader();
            bspLoader.LoadBspFile("data/BspDemo.bsp");
            BspConverter bsp2Bullet = new BspToBulletConverter(this);
            bsp2Bullet.ConvertBsp(bspLoader, 0.1f);

            World.AddCollisionObject(ghostObject, CollisionFilterGroups.CharacterFilter, CollisionFilterGroups.StaticFilter | CollisionFilterGroups.DefaultFilter);

            World.AddAction(character);
        }

        public override void ClientResetScene()
        {
            World.Broadphase.OverlappingPairCache.CleanProxyFromPairs(ghostObject.BroadphaseHandle, World.Dispatcher);

            character.Reset(World);
            // WTF
            Vector3 warp = new Vector3(10.210001f, -2.0306311f, 16.576973f);
            character.Warp(ref warp);
        }

        public override void OnHandleInput()
        {
            Matrix xform = ghostObject.WorldTransform;

            Vector3 forwardDir = new Vector3(xform.M31, xform.M32, xform.M33);
            //Console.Write("forwardDir={0},{1},{2}\n", forwardDir[0], forwardDir[1], forwardDir[2]);
            Vector3 upDir = new Vector3(xform.M21, xform.M22, xform.M23);
            forwardDir.Normalize();
            upDir.Normalize();
            Vector3 pos = xform.Origin;

            Vector3 walkDirection = Vector3.Zero;
            const float walkVelocity = 1.1f * 4.0f;
            float walkSpeed = walkVelocity * FrameDelta * 10;// * 0.0001f;
            float turnSpeed = FrameDelta * 3;

            if (Input.KeysDown.Contains(Keys.Left))
            {
                Matrix orn = xform;
                orn.Row4 = new Vector4(0, 0, 0, 1);
                orn *= Matrix.RotationAxis(upDir, -turnSpeed);
                orn.Row4 = new Vector4(pos.X, pos.Y, pos.Z, 1);
                ghostObject.WorldTransform = orn;
            }
            if (Input.KeysDown.Contains(Keys.Right))
            {
                Matrix orn = xform;
                orn.Row4 = new Vector4(0, 0, 0, 1);
                orn *= Matrix.RotationAxis(upDir, turnSpeed);
                orn.Row4 = new Vector4(pos.X, pos.Y, pos.Z, 1);
                ghostObject.WorldTransform = orn;
            }

            if (Input.KeysDown.Contains(Keys.Up))
            {
                walkDirection += forwardDir;
            }
            if (Input.KeysDown.Contains(Keys.Down))
            {
                walkDirection -= forwardDir;
            }

            Vector3 cameraPos = pos - forwardDir * 12 + upDir * 5;

            //use the convex sweep test to find a safe position for the camera (not blocked by static geometry)
            SphereShape cameraSphere = new SphereShape(0.2f);
            ClosestConvexResultCallback cb = new ClosestConvexResultCallback(ref pos, ref cameraPos);
            cb.CollisionFilterMask = (short)CollisionFilterGroups.StaticFilter;
            Matrix posMatrix = Matrix.Translation(pos);
            Matrix cameraPosMatrix = Matrix.Translation(cameraPos);
            World.ConvexSweepTestRef(cameraSphere, ref posMatrix, ref cameraPosMatrix, cb);
            cameraSphere.Dispose();
            if (cb.HasHit)
            {
                cameraPos = Vector3.Lerp(pos, cameraPos, cb.ClosestHitFraction);
            }
            cb.Dispose();
            Freelook.SetEyeTarget(cameraPos, pos);

            character.SetWalkDirection(walkDirection * walkSpeed);

            base.OnHandleInput();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            using (Demo demo = new CharacterDemo())
            {
                LibraryManager.Initialize(demo);
            }
        }
    }
}
