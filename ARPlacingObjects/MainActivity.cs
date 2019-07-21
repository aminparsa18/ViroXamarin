using System;
using System.Collections.Generic;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using ViroCore;
using AlertDialog = Android.App.AlertDialog;
using Uri = Android.Net.Uri;

namespace ARPlacingObjects
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ViroViewARCore.IStartupListener, IARHitTestListener
    {
        private static string TAG = typeof(MainActivity).Name;

        // Constants used to determine if plane or point is within bounds. Units in meters.
        private static float MIN_DISTANCE = 0.2f;
        private static float MAX_DISTANCE = 10f;
        private static ViroView mViroView;

        /**
         * The ARScene we will be creating within this activity.
         */
        private static ARScene mScene;

        /**
         * List of draggable 3D objects in our scene.
         */
        private List<Draggable3DObject> mDraggableObjects;

        private static Vector cameraPos;
        private string fileName;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            mDraggableObjects = new List<Draggable3DObject>();
            mViroView = new ViroViewARCore(this, this);
            SetContentView(mViroView);
        }

        public void OnFailure(ViroViewARCore.StartupError p0, string p1)
        {
            Log.Error(TAG, "Error initializing AR [" + p1 + "]");
        }

        public void OnSuccess()
        {
            DisplayScene();
        }

        private void DisplayScene()
        {
            mScene = new ARScene();
            // Add a listener to the scene so we can update the 'AR Initialized' text.
            mScene.SetListener(new ARSceneListener(this, mViroView));
            // Add a light to the scene so our models show up
            mScene.RootNode.AddLight(new AmbientLight(Color.White, 1000f));
            mViroView.Scene = mScene;

                var view = View.Inflate(this, Resource.Layout.viro_view_ar_hit_test_hud, mViroView);
                view.FindViewById<ImageButton>(Resource.Id.imageButton).Click += delegate { showPopup(); };
        }

        /**
    * Perform a hit-test and place the object (identified by its file name) at the intersected
    * location.
    *
    * @param fileName The resource name of the object to place.
    */
        private void placeObject(string fileName)
        {
            this.fileName = fileName;
            var viewARView = (ViroViewARCore) mViroView;
            cameraPos = viewARView.LastCameraPositionRealtime;
            viewARView.PerformARHitTestWithRay(viewARView.LastCameraForwardRealtime, this);
        }

        /**
         * Add a 3D object with the given filename to the scene at the specified world position.
         */
        private void add3DDraggableObject(string filename, Vector position)
        {
            var draggable3DObject = new Draggable3DObject(filename);
            mDraggableObjects.Add(draggable3DObject);
            draggable3DObject.addModelToPosition(position);
        }

        /**
         * Dialog menu displaying the virtual objects we can place in the real world.
         */
        public void showPopup()
        {
            var builder = new AlertDialog.Builder(this);
            var itemsList = new[] {"Coffee mug", "Flowers", "Smile Emoji"};
            builder.SetTitle("Choose an object")
                .SetItems(itemsList,
                    (s, e) =>
                    {
                        switch (e.Which)
                        {
                            case 0:
                                placeObject("file:///android_asset/object_coffee_mug.vrx");
                                break;
                            case 1:
                                placeObject("file:///android_asset/object_flowers.vrx");
                                break;
                            case 2:
                                placeObject("file:///android_asset/emoji_smile.vrx");
                                break;
                        }
                    });
            Dialog d = builder.Create();
            d.Show();
        }

        private class ARSceneListener : Java.Lang.Object, ARScene.IListener
        {
            private WeakReference mCurrentActivityWeak;
            private bool mInitialized;

            public ARSceneListener(Activity activity, View rootView)
            {
                mCurrentActivityWeak = new WeakReference(activity);
                mInitialized = false;
            }

            public void OnAmbientLightUpdate(float p0, Vector p1)
            {
            }

            public void OnAnchorFound(ARAnchor p0, ARNode p1)
            {
            }

            public void OnAnchorRemoved(ARAnchor p0, ARNode p1)
            {
            }

            public void OnAnchorUpdated(ARAnchor p0, ARNode p1)
            {
            }

            public void OnTrackingInitialized()
            {
            }

            public void OnTrackingUpdated(ARScene.TrackingState p0, ARScene.TrackingStateReason p1)
            {
                if (!mInitialized && p0 == ARScene.TrackingState.Normal)
                {
                    var activity = (Activity) mCurrentActivityWeak.Target;
                    if (activity == null)
                    {
                        return;
                    }

                    var initText = activity.FindViewById<TextView>(Resource.Id.initText);
                    initText.Text = "AR is initialized";
                    mInitialized = true;
                }
            }
        }

        private class Draggable3DObject : Java.Lang.Object, IAsyncObject3DListener
        {
            private string mFileName;
            private float rotateStart;
            private float scaleStart;

            public Draggable3DObject(string filename)
            {
                mFileName = filename;
            }

            public void addModelToPosition(Vector position)
            {
                var object3D = new Object3D();
                object3D.SetPosition(position);
                // Shrink the objects as the original size is too large.
                object3D.SetScale(new Vector(.2f, .2f, .2f));
                object3D.GestureRotate += (s, e) =>
                {
                    if (e.P3 == RotateState.RotateStart)
                    {
                        rotateStart = object3D.RotationEulerRealtime.Y;
                    }

                    var totalRotationY = rotateStart + e.P2;
                    object3D.SetRotation(new Vector(0, totalRotationY, 0));
                };
                object3D.GesturePinch += (s, e) =>
                {
                    if (e.P3 == PinchState.PinchStart)
                    {
                        scaleStart = object3D.ScaleRealtime.X;
                    }
                    else
                    {
                        object3D.SetScale(new Vector(scaleStart * e.P2, scaleStart * e.P2, scaleStart * e.P2));
                    }
                };
                object3D.Drag += (s, e) => { };
                object3D.LoadModel(mViroView.ViroContext, Uri.Parse(mFileName), Object3D.Type.Fbx, this);
                // Make the object draggable.
                object3D.SetDragType(Node.DragType.FixedToWorld);
                mScene.RootNode.AddChildNode(object3D);
            }

            public void OnObject3DFailed(string p0)
            {
                Toast.MakeText(Application.Context, "An error occured when loading the 3D Object!",
                    ToastLength.Long).Show();
            }

            public void OnObject3DLoaded(Object3D p0, Object3D.Type p1)
            {
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            mViroView.OnActivityStarted(this);
        }

        protected override void OnStop()
        {
            base.OnStop();
            mViroView.OnActivityStopped(this);
        }

        protected override void OnResume()
        {
            base.OnResume();
            mViroView.OnActivityResumed(this);
        }

        protected override void OnPause()
        {
            base.OnPause();
            mViroView.OnActivityPaused(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            mViroView.OnActivityDestroyed(this);
        }

        public void OnHitTestFinished(ARHitTestResult[] p0)
        {
            if (p0 != null && p0.Length > 0)
            {
                for (var i = 0; i < p0.Length; i++)
                {
                    var result = p0[i];
                    var distance = result.Position.Distance(cameraPos);
                    if (!(distance > MIN_DISTANCE) || !(distance < MAX_DISTANCE)) continue;
                    // If we found a plane or feature point further than 0.2m and less 10m away,
                    // then choose it!
                    add3DDraggableObject(fileName, result.Position);
                    return;
                }
            }

            Toast.MakeText(ApplicationContext, "Unable to find suitable point or plane to place object!",
            ToastLength.Long).Show();
        }
    }
}