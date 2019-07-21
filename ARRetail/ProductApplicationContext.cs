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
using ARRetail.Model;

namespace ARRetail
{
    [Application]
   public class ProductApplicationContext:Application
    {
        private ProductManagerDb mProductDB;

        public ProductManagerDb GetProductDb()
        {
            return mProductDB ?? (mProductDB = new ProductManagerDb());
        }
    }
}