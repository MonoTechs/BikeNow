﻿using System;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.OS;

namespace BikeNow
{
	public abstract class BaseActivity : AppCompatActivity
	{
		public Toolbar Toolbar {
			get;
			set;
		}
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView (LayoutResource);
			Toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
			if (Toolbar != null) {
				SetSupportActionBar(Toolbar);
				SupportActionBar.SetDisplayHomeAsUpEnabled(true);
				SupportActionBar.SetHomeButtonEnabled (true);

			}
		}

		protected abstract int LayoutResource{
			get;
		}

		protected int ActionBarIcon {
			set{ Toolbar.SetNavigationIcon (value); }
		}
	}
}

