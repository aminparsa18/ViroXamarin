using Android.App;
using Android.OS;
using Android.Views;
using Android.Webkit;

namespace ARRetail
{
    [Activity(Label = "ProductDetailActivity")]
    public class ProductDetailActivity:Activity
    {
        private WebView mWebView;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_product_detail);

            mWebView = FindViewById<WebView>(Resource.Id.webview);
            var webSettings = mWebView.Settings;
            webSettings.JavaScriptEnabled=true;

            var intent = Intent;
            var key = intent.GetStringExtra(MainActivity.IntentProductKey);
            var context =new ProductApplicationContext();
            var selectedProduct = context.GetProductDb().GetProductByName(key);
            mWebView.LoadUrl(selectedProduct.UrlWebPage);

            var actionBar = ActionBar;
            //actionBar.SetDisplayHomeAsUpEnabled(true);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            Finish();
            return true;
        }

        public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
        {
            // Check if the key event was the Back button and if there's history
            if (keyCode == Keycode.Back && mWebView.CanGoBack())
            {
                mWebView.GoBack();
                return true;
            }
            // If it wasn't the Back key or there's no web page history, bubble up to the default
            // system behavior (probably exit the activity)
            return base.OnKeyDown(keyCode,e);
        }
    }
}