using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using ARRetail.Model;

namespace ARRetail
{
    [Activity(Label = "ProductSelectionActivity", MainLauncher = true, ConfigurationChanges =
        ConfigChanges.KeyboardHidden |
        ConfigChanges.Orientation|
        ConfigChanges.ScreenSize,ScreenOrientation = ScreenOrientation.Portrait)]

public class ProductSelectionActivity : Activity
    {
        private ProductAdapter mProductAdapter;
        private static CategoryAdapter mCategoryAdapter;
        public static string IntentProductKey = "product_key";
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.product_activity_main_layout);

            // Init the scroll view, containing a list of categories, on the top of the layout
            SetupCategoryScrollView();

            // Init the product grid view below the category scroll view. This will contain all
            // the products pertaining to the selected cateogry.
            SetupProductGridView();
        }

        protected override void OnResume()
        {
            base.OnResume();
            // Refresh the list views
            mCategoryAdapter.Invalidate();
            mProductAdapter.Invalidate();
        }

        private void SetupCategoryScrollView()
        {
            // Bind known categories to the horizontal recycler view
            mCategoryAdapter = new CategoryAdapter();
            var categoryView = FindViewById<RecyclerView>(Resource.Id.categorial_list_view);
            categoryView.SetAdapter(mCategoryAdapter);

            var manager =
                new LinearLayoutManager(this, LinearLayoutManager.Horizontal, false);
            categoryView.SetLayoutManager(manager);

            // If a cateogry is clicked, refresh the product grid view with the latest products.
            mCategoryAdapter.ItemClick += (s, e) =>
            {
                var selectedCategory = (Category) (s as View).Tag;
                mProductAdapter.SetSelectedCategory(selectedCategory);
            };
        }

        private void SetupProductGridView()
        {
            // Bind the product data to the gridview.
            mProductAdapter = new ProductAdapter(this);
            var gridview = FindViewById<GridView>(Resource.Id.product_grid_view);
            gridview.Adapter = mProductAdapter;
            // If a product is selected, enter AR mode with the selected product.
            gridview.ItemClick += (s, e) =>
            {
                var selectedProduct = mProductAdapter[e.Position];
                var intent = new Intent(this, typeof(MainActivity));
                intent.PutExtra(IntentProductKey, selectedProduct.Name);
                StartActivity(intent);
            };
        }

        public class CategoryAdapter : RecyclerView.Adapter
        {
            private List<Category> mCatList = new List<Category>();
            private readonly ProductApplicationContext mContext;
            public event EventHandler<int> ItemClick;
            private int mSelectedCategoryIndex = -1;

            public CategoryAdapter()
            {
                mContext =new ProductApplicationContext();
                mSelectedCategoryIndex = -1;
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                var vh = holder as MyViewHolder;
                var cat = mCatList[position];
               // var drawable = ContextCompat.GetDrawable(mContext, cat.Image);
                vh.ImageView.SetImageResource(cat.Image);
                vh.Txtview.Text = cat.Name;
                vh.ImageView.Click += (s, e) =>
                {
                    var category = mCatList[position];
                    (s as View).Tag = category;
                    ItemClick.Invoke(s, position);
                    mSelectedCategoryIndex = position;
                    mCategoryAdapter.Invalidate();
                };
                // Note: If cateogry is selected, show highlighted area
                vh.HighlightView.Visibility = mSelectedCategoryIndex == position ? ViewStates.Visible : ViewStates.Gone;
            }

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                var itemView = LayoutInflater.From(
                    parent.Context).Inflate(
                    Resource.Layout.product_activity_category_layout, parent, false);
                return new MyViewHolder(itemView);
            }

            public override int ItemCount => mCatList.Count;

            public void Invalidate()
            {
                // Grab category data from the db.
                mCatList = mContext.GetProductDb().GetCatlist();

                // Notify data set has changed.
                NotifyDataSetChanged();
            }

            public class MyViewHolder : RecyclerView.ViewHolder
            {
                public ImageView ImageView;
                public TextView Txtview;
                public ImageView HighlightView;

                public MyViewHolder(View itemView) : base(itemView)
                {
                    ImageView = itemView.FindViewById<ImageView>(Resource.Id.category_image);
                    Txtview = itemView.FindViewById<TextView>(Resource.Id.category_name);
                    HighlightView = itemView.FindViewById<ImageView>(Resource.Id.category_highlight);
                }
            }
        }

        public class ProductAdapter : BaseAdapter<Product>
        {
            private readonly ProductApplicationContext mAppContext;
            private List<Product> mProductList = new List<Product>();
            private Category mCurrentCat;

            public ProductAdapter(Context context)
            {
                mAppContext =new ProductApplicationContext();
            }

            public void SetSelectedCategory(Category category)
            {
                mCurrentCat = category;
                Invalidate();
            }

            public override Product this[int position] => mProductList[position];

            public override long GetItemId(int position)
            {
                return position;
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                if (convertView == null)
                {
                    convertView = LayoutInflater.From(
                        parent.Context).Inflate(
                        Resource.Layout.product_activity_item_layout, parent, false);
                }

                var product = mProductList[position];
                var image = convertView.FindViewById<ImageView>(Resource.Id.product_image);
                image.SetImageResource(product.Image);

                return convertView;
            }

            public void Invalidate()
            {
                if (mCurrentCat == null)
                {
                    mProductList.Clear();
                }
                else
                {
                    mProductList = mAppContext.GetProductDb().GetProductList(mCurrentCat);
                }

                NotifyDataSetChanged();
            }

            public override int Count => mProductList.Count;
        }
    }
}