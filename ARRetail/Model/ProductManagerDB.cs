using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace ARRetail.Model
{
    public class ProductManagerDb
    {
        // A list of all known categories
        private readonly List<Category> mCatList = new List<Category>();

        // A Map of Categories to product lists.
        private readonly Dictionary<string, List<Product>> mProducts = new Dictionary<string, List<Product>>();

        private static readonly string lampWebpage = "https://www.amazon.com/Simple-Designs-LT2007-PRP-Chrome-Fabric/dp/B00CM5SVU2/ref=sr_1_9?s=hi&ie=UTF8&qid=1515010381&sr=1-9";

    // A static set of all known products to be displayed in the application.
    private readonly Product[] defaultProductList = {
            new Product{Name="Furniture",Image = Resource.Drawable.furniture_0,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage = lampWebpage},
            new Product{Name = "Furniture",Image = Resource.Drawable.furniture_1,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage = lampWebpage},
            new Product{Name = "Furniture",Image = Resource.Drawable.furniture_2,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage = lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_3,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_4,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_5,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_6,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_7,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_8,ThreeDModelUri = "file:///android_asset/object_lamp.vrx", UrlWebPage = lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_9,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_10,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_11,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_12,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_13,ThreeDModelUri = "file:///android_asset/object_lamp.vrx", UrlWebPage = lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_14,ThreeDModelUri = "file:///android_asset/object_lamp.vrx", UrlWebPage = lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_15,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage},
            new Product{Name ="Furniture", Image = Resource.Drawable.furniture_16,ThreeDModelUri = "file:///android_asset/object_lamp.vrx",UrlWebPage =  lampWebpage}
    };

        public ProductManagerDb()
        {
            // Create a static list of products to be accessed through this "DB".
            CreateCateogryForProduct("Top Picks", Resource.Drawable.furniture_17, CreateRandomizedProductList());
            CreateCateogryForProduct("Chairs", Resource.Drawable.furniture_18, CreateRandomizedProductList());
            CreateCateogryForProduct("Sofas", Resource.Drawable.furniture_19, CreateRandomizedProductList());
            CreateCateogryForProduct("Lamps", Resource.Drawable.furniture_20, CreateRandomizedProductList());
            CreateCateogryForProduct("Beds", Resource.Drawable.furniture_21, CreateRandomizedProductList());
        }

        private void CreateCateogryForProduct(string catName, int icon, List<Product> list)
        {
            mCatList.Add(new Category{Name = catName,Image = icon});
            mProducts.Add(catName, list);
        }

        private List<Product> CreateRandomizedProductList()
        {
            var r = new Random();
            var addedList = new List<int>();
            var productList = new List<Product>();
            for (var i = 0; i < defaultProductList.Length; i++)
            {
                var randProduct = r.Next(defaultProductList.Length);

                while (addedList.Contains(randProduct))
                {
                    randProduct = r.Next(defaultProductList.Length);
                }

                addedList.Add(randProduct);
                productList.Add(defaultProductList[randProduct]);
            }
            return productList;
        }

        public List<Category> GetCatlist()
        {
            return mCatList;
        }

        public List<Product> GetProductList(Category cat)
        {
            return mProducts[cat.Name];
        }

        public Product GetProductByName(string name)
        {
            foreach (var list in mProducts.Values)
            {
                foreach (var product in list)
                {
                    if (product.Name==name)
                    {
                        return product;
                    }
                }
            }
            return null;
        }
    }

}