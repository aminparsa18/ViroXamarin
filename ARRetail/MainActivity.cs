using System.Collections.Generic;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using ARRetail.Model;
using Java.IO;
using ViroCore;
using Surface = ViroCore.Surface;

namespace ARRetail
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", ConfigurationChanges =
        ConfigChanges.KeyboardHidden |
        ConfigChanges.Orientation |
        ConfigChanges.ScreenSize)]
    public class MainActivity : Activity, IARHitTestListener, ViroViewARCore.IStartupListener,
        ViroMediaRecorder.IScreenshotFinishListener, ARScene.IListener
    {
        private static readonly int recordPermKey = 50;
        private static readonly string tag = typeof(MainActivity).Name;
        public static string IntentProductKey = "product_key";

        private ViroViewARCore mViroView;
        private ARScene mScene;
        private View mHudGroupView;
        private TextView mHudInstructions;
        private ImageView mCameraButton;
        private View mIconShakeView;

        /*
         The Tracking status is used to coordinate the displaying of our 3D controls and HUD
         UI as the user looks around the tracked AR Scene.
         */
        private enum TrackStatus
        {
            FindingSurface,
            SurfaceNotFound,
            SurfaceFound,
            SelectedSurface
        }

        private TrackStatus mStatus = TrackStatus.SurfaceNotFound;
        private Product mSelectedProduct = null;
        private Node mProductModelGroup = null;
        private Node mCrosshairModel = null;
        private AmbientLight mMainLight = null;
        private Vector mLastProductRotation = new Vector();
        private Vector mSavedRotateToRotation = new Vector();

        /*
         * ARNode under which to parent our 3D furniture model. This is only created
         * and non-ull if a user has selected a surface upon which to place the furniture.
         */
        private ARNode mHitArNode = null;

        private bool mInitialized = false;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RendererConfiguration config = new RendererConfiguration
            {
                ShadowsEnabled = true, BloomEnabled = true, HDREnabled = true, PBREnabled = true
            };
            mViroView = new ViroViewARCore(this, this);
            SetContentView(mViroView);

            var intent = Intent;
            string key = intent.GetStringExtra(IntentProductKey);
            ProductApplicationContext context = new ProductApplicationContext();
            mSelectedProduct = context.GetProductDb().GetProductByName(key);

            View.Inflate(this, Resource.Layout.ar_hud, mViroView);
            mHudGroupView = FindViewById<View>(Resource.Id.main_hud_layout);
            mHudGroupView.Visibility = ViewStates.Gone;
        }

        protected override void OnStart()
        {
            base.OnStart();
            mViroView.OnActivityStarted(this);
        }

        protected override void OnPause()
        {
            base.OnPause();
            mViroView.OnActivityPaused(this);
        }

        protected override void OnResume()
        {
            base.OnResume();
            mViroView.OnActivityResumed(this);
        }

        protected override void OnStop()
        {
            base.OnStop();
            mViroView.OnActivityStopped(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            mViroView.OnActivityDestroyed(this);
        }

        private void RequestPermissions()
        {
            ActivityCompat.RequestPermissions(this,
                new[] {Manifest.Permission.WriteExternalStorage},
                recordPermKey);
        }

        private static bool HasRecordingStoragePermissions(Context context)
        {
            var hasExternalStoragePerm = ContextCompat.CheckSelfPermission(context,
                                             Manifest.Permission.WriteExternalStorage) == Permission.Granted;
            return hasExternalStoragePerm;
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
            Permission[] grantResults)
        {
            if (requestCode == recordPermKey && !HasRecordingStoragePermissions(this))
            {
                Toast toast = Toast.MakeText(ApplicationContext, "User denied permissions", ToastLength.Long);
                toast.Show();
            }
        }

        private void DisplayScene()
        {
            // Create the ARScene within which to load our ProductAR Experience
            mScene = new ARScene();
            mMainLight = new AmbientLight(Color.ParseColor("#606060"), 400);
            mMainLight.InfluenceBitMask = 3;
            mScene.RootNode.AddLight(mMainLight);

            // Setup our 3D and HUD controls
            InitArCrosshair();
            Init3DModelProduct();
            InitArHud();

            // Start our tracking UI when the scene is ready to be tracked
            mScene.SetListener(this);

            // Finally set the arScene on the renderer
            mViroView.Scene = mScene;
        }

        private void InitArHud()
        {
            // TextView instructions
            mHudInstructions = mViroView.FindViewById<TextView>(Resource.Id.ar_hud_instructions);
            mViroView.FindViewById(Resource.Id.bottom_frame_controls).Visibility = ViewStates.Visible;

            // Bind the back button on the top left of the layout
            ImageView view = FindViewById<ImageView>(Resource.Id.ar_back_button);
            view.Click += delegate { Finish(); };
            // Bind the detail buttons on the top right of the layout.
            ImageView productDetails = FindViewById<ImageView>(Resource.Id.ar_details_page);
            productDetails.Click += delegate
            {
                Intent intent = new Intent(this, typeof(ProductDetailActivity));
                intent.PutExtra(IntentProductKey, mSelectedProduct.Name);
                StartActivity(intent);
            };


            // Bind the camera button on the bottom, for taking images.
            mCameraButton = mViroView.FindViewById<ImageView>(Resource.Id.ar_photo_button);
            File photoFile = new File(FilesDir, "screenShot");
            mCameraButton.Click += delegate
            {
                if (!HasRecordingStoragePermissions(BaseContext))
                {
                    RequestPermissions();
                    return;
                }

                mViroView.Recorder.TakeScreenShotAsync("screenShot", true, this);
            };

            mIconShakeView = mViroView.FindViewById(Resource.Id.icon_shake_phone);
        }

        private void InitArCrosshair()
        {
            if (mCrosshairModel != null)
            {
                return;
            }

            AmbientLight am = new AmbientLight {InfluenceBitMask = 2, Intensity = 1000};
            mScene.RootNode.AddLight(am);
            Object3D crosshairModel = new Object3D();
            mScene.RootNode.AddChildNode(crosshairModel);
            crosshairModel.LoadModel(mViroView.ViroContext, Uri.Parse("file:///android_asset/tracking_1.vrx"),
                Object3D.Type.Fbx, new AsyncObject3DListener1(this));
        }

        private class AsyncObject3DListener1 : Java.Lang.Object, IAsyncObject3DListener
        {
            private readonly MainActivity activity;

            public AsyncObject3DListener1(MainActivity activity)
            {
                this.activity = activity;
            }

            public void OnObject3DFailed(string p0)
            {
                Log.Error("Viro", " Model load failed : " + p0);
            }

            public void OnObject3DLoaded(Object3D p0, Object3D.Type p1)
            {
                activity.mCrosshairModel = p0;
                activity.mCrosshairModel.Opacity = 0;
                p0.LightReceivingBitMask = 2;
                activity.mCrosshairModel.SetScale(new Vector(0.175, 0.175, 0.175));
                activity.mCrosshairModel.Click += (s, e) =>
                {
                    activity.SetTrackingStatus(TrackStatus.SelectedSurface);
                };
                activity.mCrosshairModel.ClickState += (s, e) => { };
            }
        }

        private void Init3DModelProduct()
        {
            // Create our group node containing the light, shadow plane, and 3D models
            mProductModelGroup = new Node();

            // Create a light to be shined on the model.
            Spotlight spotLight = new Spotlight();
            spotLight.InfluenceBitMask = 1;
            spotLight.Position = new Vector(0, 5, 0);
            spotLight.CastsShadow = true;
            spotLight.AttenuationEndDistance = 7;
            spotLight.AttenuationStartDistance = 4;
            spotLight.Direction = new Vector(0, -1, 0);
            spotLight.Intensity = 6000;
            spotLight.ShadowOpacity = 0.35f;
            mProductModelGroup.AddLight(spotLight);

            // Create a mock shadow plane in AR
            Node shadowNode = new Node();
            var shadowSurface = new Surface(20, 20);
            Material material = new Material();
            material.SetShadowMode(Material.ShadowMode.Transparent);
            material.SetLightingModel(Material.LightingModel.Lambert);
            shadowSurface.Materials = new List<Material>() {material};
            shadowNode.Geometry = shadowSurface;
            shadowNode.LightReceivingBitMask = 1;
            shadowNode.SetPosition(new Vector(0, -0.01, 0));
            shadowNode.SetRotation(new Vector(-1.5708, 0, 0));
            // We want the shadow node to ignore all events because it contains a surface of size 20x20
            // meters and causes this to capture events which will bubble up to the mProductModelGroup node.
            shadowNode.IgnoreEventHandling = true;
            mProductModelGroup.AddChildNode(shadowNode);

            // Load the model from the given mSelected Product
            Object3D productModel = new Object3D();
            productModel.LoadModel(mViroView.ViroContext, Uri.Parse(mSelectedProduct!=null?mSelectedProduct.ThreeDModelUri:""), Object3D.Type.Fbx,
                new AsyncObject3DListener2(this));

            // Make this 3D Product object draggable.
            mProductModelGroup.SetDragType(Node.DragType.FixedToWorld);
            mProductModelGroup.Drag += (s, e) => { };
            // Set gesture listeners such that the user can rotate this model.
            productModel.GestureRotate += (s, e) =>
            {
                if (e.P3 == RotateState.RotateEnd)
                {
                    mLastProductRotation = mSavedRotateToRotation;
                }
                else
                {
                    Vector rotateTo = new Vector(mLastProductRotation.X, mLastProductRotation.Y + e.P2,
                        mLastProductRotation.Z);
                    mProductModelGroup.SetRotation(rotateTo);
                    mSavedRotateToRotation = rotateTo;
                }
            };

            mProductModelGroup.Opacity = 0;
            mProductModelGroup.AddChildNode(productModel);
        }

        private class AsyncObject3DListener2 : Java.Lang.Object, IAsyncObject3DListener
        {
            private readonly MainActivity activity;

            public AsyncObject3DListener2(MainActivity activity)
            {
                this.activity = activity;
            }

            public void OnObject3DFailed(string p0)
            {
                Log.Error("Viro", " Model load failed : " + p0);
            }

            public void OnObject3DLoaded(Object3D p0, Object3D.Type p1)
            {
                p0.LightReceivingBitMask = 1;
                activity.mProductModelGroup.Opacity = 0;
                activity.mProductModelGroup.SetScale(new Vector(0.9, 0.9, 0.9));
                activity.mLastProductRotation = p0.RotationEulerRealtime;
            }
        }

        public void OnHitTestFinished(ARHitTestResult[] p0)
        {
            if (p0 == null || p0.Length <= 0)
            {
                return;
            }

            // If we have found intersected AR Hit points, update views as needed, reset miss count.
            ViroViewARCore viewArView = mViroView;
            var cameraPos = viewArView.LastCameraPositionRealtime;

            // Grab the closest ar hit target
            float closestDistance = float.MaxValue;
            ARHitTestResult result = null;
            for (int i = 0; i < p0.Length; i++)
            {
                ARHitTestResult currentResult = p0[i];

                float distance = currentResult.Position.Distance(cameraPos);
                if (distance < closestDistance && distance > .3 && distance < 5)
                {
                    result = currentResult;
                    closestDistance = distance;
                }
            }

            // Update the cross hair target location with the closest target.
            if (result != null)
            {
                mCrosshairModel.SetPosition(result.Position);
                mCrosshairModel.SetRotation(result.Rotation);
            }

            // Update State based on hit target
            SetTrackingStatus(result != null ? TrackStatus.SurfaceFound : TrackStatus.FindingSurface);
        }

        private void SetTrackingStatus(TrackStatus status)
        {
            if (mStatus == TrackStatus.SelectedSurface || mStatus == status)
            {
                return;
            }

            // If the surface has been selected, we no longer need our cross hair listener.
            if (status == TrackStatus.SelectedSurface)
            {
                mViroView.SetCameraARHitTestListener(null);
            }

            mStatus = status;
            UpdateUiHud();
            Update3DarCrosshair();
            Update3DModelProduct();
        }

        private void UpdateUiHud()
        {
            switch (mStatus)
            {
                case TrackStatus.FindingSurface:
                    mHudInstructions.Text = "Point the camera at the flat surface where you want to view your product.";
                    break;
                case TrackStatus.SurfaceNotFound:
                    mHudInstructions.Text =
                        "We can’t seem to find a surface. Try moving your phone more in any direction.";
                    break;
                case TrackStatus.SurfaceFound:
                    mHudInstructions.Text = "Great! Now tap where you want to see the product.";
                    break;
                case TrackStatus.SelectedSurface:
                    mHudInstructions.Text = "Great! Use one finger to move and two fingers to rotate.";
                    break;
                default:
                    mHudInstructions.Text = "Initializing AR....";
                    break;
            }

            // Update the camera UI
            mCameraButton.Visibility = mStatus == TrackStatus.SelectedSurface ? ViewStates.Visible : ViewStates.Gone;

            // Update the Icon shake view
            mIconShakeView.Visibility = mStatus == TrackStatus.SurfaceNotFound ? ViewStates.Visible : ViewStates.Gone;
        }

        private void Update3DarCrosshair()
        {
            switch (mStatus)
            {
                case TrackStatus.FindingSurface:
                case TrackStatus.SurfaceNotFound:
                case TrackStatus.SelectedSurface:
                    mCrosshairModel.Opacity = 0;
                    break;
                case TrackStatus.SurfaceFound:
                    mCrosshairModel.Opacity = 1;
                    break;
            }

            if (mStatus == TrackStatus.SelectedSurface)
            {
                mViroView.SetCameraARHitTestListener(null);
            }
        }

        private void Update3DModelProduct()
        {
            // Hide the product if the user has not placed it yet.
            if (mStatus != TrackStatus.SelectedSurface)
            {
                mProductModelGroup.Opacity = 0;
                return;
            }

            if (mHitArNode != null)
            {
                return;
            }

            mHitArNode = mScene.CreateAnchoredNode(mCrosshairModel.PositionRealtime);
            mHitArNode.AddChildNode(mProductModelGroup);
            mProductModelGroup.Opacity = 1;
        }

        public void OnFailure(ViroViewARCore.StartupError p0, string p1)
        {
            Log.Error(tag, "Failed to load AR Scene [" + p1 + "]");
        }

        public void OnSuccess()
        {
            DisplayScene();
        }

        public void OnError(ViroMediaRecorder.Error p0)
        {
            Log.Error("Viro", "onTaskFailed " + p0);
        }

        public void OnSuccess(Bitmap p0, string p1)
        {
            Intent shareIntent = new Intent(Intent.ActionSend);
            shareIntent.SetType("image/png");
            shareIntent.PutExtra(Intent.ExtraStream, Uri.Parse(p1));
            StartActivity(Intent.CreateChooser(shareIntent, "Share image using"));
        }

        public void OnAmbientLightUpdate(float p0, Vector p1)
        {
            // no-op
        }

        public void OnAnchorFound(ARAnchor p0, ARNode p1)
        {
            // no-op
        }

        public void OnAnchorRemoved(ARAnchor p0, ARNode p1)
        {
            // no-op
        }

        public void OnAnchorUpdated(ARAnchor p0, ARNode p1)
        {
            // no-op
        }

        public void OnTrackingInitialized()
        {
            // This method is deprecated.
        }

        public void OnTrackingUpdated(ARScene.TrackingState p0, ARScene.TrackingStateReason p1)
        {
            if (p0 == ARScene.TrackingState.Normal && !mInitialized)
            {
                // The Renderer is ready - turn everything visible.
                mHudGroupView.Visibility = ViewStates.Visible;

                // Update our UI views to the finding surface state.
                SetTrackingStatus(TrackStatus.FindingSurface);
                mInitialized = true;
            }
        }
    }
}