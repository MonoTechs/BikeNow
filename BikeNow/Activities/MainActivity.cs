using System;
using System.Linq;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Locations;

using Android.Gms.Common;
using Android.Gms.Common.Apis;
using ConnectionCallbacks = Android.Gms.Common.Apis.IGoogleApiClientConnectionCallbacks;
using ConnectionFailedListener = Android.Gms.Common.Apis.IGoogleApiClientOnConnectionFailedListener;
using ActionBarDrawerToggle = Android.Support.V7.App.ActionBarDrawerToggle;

using Android.Support.V4.App;
using Android.Support.V4.Widget;
using Android.Gms.Location;

namespace BikeNow
{
    [Activity(Label = "Bike Now",
        MainLauncher = true,
        Theme = "@style/BikeNowTheme",
        ScreenOrientation = ScreenOrientation.Portrait,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
        LaunchMode = LaunchMode.SingleTop)]
    [IntentFilter(new[] { "android.intent.action.SEARCH" }, Categories = new[] { "android.intent.category.DEFAULT" })]
    [MetaData("android.app.searchable", Resource = "@xml/searchable")]
    public class MainActivity
		: BaseActivity, IObserver<Station[]>, ConnectionCallbacks, ConnectionFailedListener
    {
        const int ConnectionFailureResolutionRequest = 9000;

        ProntoMapFragment mapFragment;
        FavoriteFragment favoriteFragment;
        RentalFragment rentalFragment;

        DrawerLayout drawer;
        Android.Support.V7.App.ActionBarDrawerToggle drawerToggle;
        ListView drawerMenu;
        ListView drawerAround;

        DrawerAroundAdapter aroundAdapter;
        IGoogleApiClient client;

        Typeface menuNormalTf, menuHighlightTf;

        Android.Support.V4.App.Fragment CurrentFragment
        {
            get
            {
                return new Android.Support.V4.App.Fragment[]
                {
                    mapFragment,
                    favoriteFragment,
                    rentalFragment
                }.FirstOrDefault(f => f != null && f.IsAdded && f.IsVisible);
            }
        }

        protected override int LayoutResource
        {
            get
            {
                return Resource.Layout.Main;
            }
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Xamarin.Insights.Initialize("0d4fce398ef6007c41608174cd08dca7ea995c7a", this);
            Xamarin.Insights.ForceDataTransmission = true;
            AndroidExtensions.Initialize(this);

            this.drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            this.drawerToggle = new ProntoActionBarToggle(this,
                drawer,
                Resource.String.open_drawer,
                Resource.String.close_drawer)
            {
                OpenCallback = () =>
                {
                    SupportActionBar.Title = Title;
                    if (CurrentFragment != null)
                        CurrentFragment.HasOptionsMenu = false;
                    InvalidateOptionsMenu();
                },
                CloseCallback = () =>
                {
                    var currentFragment = CurrentFragment;
                    if (currentFragment != null)
                    {
                        SupportActionBar.Title = ((IProntoSection)currentFragment).Title;
                        currentFragment.HasOptionsMenu = true;
                    }
                    InvalidateOptionsMenu();
                },
            };
            drawer.SetDrawerShadow(Resource.Drawable.drawer_shadow, (int)GravityFlags.Left);
            drawer.SetDrawerListener(drawerToggle);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);

            Pronto.Instance.Subscribe(this);
            FavoriteManager.FavoritesChanged += (sender, e) => aroundAdapter.Refresh();

            drawerMenu = FindViewById<ListView>(Resource.Id.left_drawer);
            drawerMenu.AddFooterView(new Android.Support.V4.Widget.Space(this));
            drawerMenu.ItemClick += HandleSectionItemClick;
            menuNormalTf = Typeface.Create(Resources.GetString(Resource.String.menu_item_fontFamily),
                TypefaceStyle.Normal);

            drawerMenu.Adapter = new DrawerMenuAdapter(this);

            drawerAround = FindViewById<ListView>(Resource.Id.left_drawer_around);
            drawerAround.ItemClick += HandleAroundItemClick;
            drawerAround.Adapter = aroundAdapter = new DrawerAroundAdapter(this);

            drawerMenu.SetItemChecked(0, true);

            if (CheckGooglePlayServices())
            {
                client = CreateApiClient();
                SwitchTo(mapFragment = new ProntoMapFragment(this));
                SupportActionBar.Title = ((IProntoSection)mapFragment).Title;
            }
        }

        IGoogleApiClient CreateApiClient()
        {
            return new GoogleApiClientBuilder(this, this, this)
                .AddApi(LocationServices.API)
				.Build();
        }

        void HandleAroundItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            if (mapFragment != null)
            {
                drawer.CloseDrawers();
                mapFragment.CenterAndOpenStationOnMap(e.Id,
                    zoom: 17,
                    animDurationID: Android.Resource.Integer.ConfigLongAnimTime);
            }
        }

        void HandleSectionItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            switch (e.Position)
            {
                case 0:
                    if (mapFragment == null)
                        mapFragment = new ProntoMapFragment(this);
                    SwitchTo(mapFragment);
                    break;
                case 1:
                    if (favoriteFragment == null)
                    {
                        favoriteFragment = new FavoriteFragment(this, id =>
                            {
                                SwitchTo(mapFragment);
                                mapFragment.CenterAndOpenStationOnMap(id,
                                    zoom: 17,
                                    animDurationID: Android.Resource.Integer.ConfigLongAnimTime);
                            });
                    }
                    SwitchTo(favoriteFragment);
                    break;
                case 2:
                    StartActivity(new Intent(this, typeof(SettingsActivity)));
                    var data = new Dictionary<string, string>();
                    data.Add("Section", "Settings");
                    Xamarin.Insights.Track("Navigated", data);
                    break;

            /*case 2:
				if (rentalFragment == null)
					rentalFragment = new RentalFragment (this);
				SwitchTo (rentalFragment);
				break;*/
                default:
                    return;
            }
            SetSelectedMenuIndex(e.Position);
            drawerMenu.SetItemChecked(e.Position, true);
            drawer.CloseDrawers();
        }

        void SwitchTo(Android.Support.V4.App.Fragment fragment)
        {
            if (fragment.IsVisible)
                return;
            var section = fragment as IProntoSection;
            if (section == null)
                return;
            var name = section.Name;

            var data = new Dictionary<string, string>();
            data.Add("Section", section.Name);
            Xamarin.Insights.Track("Navigated", data);

            var t = SupportFragmentManager.BeginTransaction();
            var currentFragment = CurrentFragment;
            if (currentFragment == null)
            {
                t.Add(Resource.Id.content_frame, fragment, name);
            }
            else
            {
                t.SetCustomAnimations(Resource.Animator.frag_slide_in,
                    Resource.Animator.frag_slide_out);
                var existingFragment = SupportFragmentManager.FindFragmentByTag(name);
                if (existingFragment != null)
                    existingFragment.View.BringToFront();
                currentFragment.View.BringToFront();
                t.Hide(CurrentFragment);
                if (existingFragment != null)
                {
                    t.Show(existingFragment);
                }
                else
                {
                    t.Add(Resource.Id.content_frame, fragment, name);
                }
                section.RefreshData();
            }
            t.Commit();
        }

        void SetSelectedMenuIndex(int pos)
        {
            for (int i = 0; i < 2; i++)
            {
                var view = drawerMenu.GetChildAt(i);
                var text = view.FindViewById<TextView>(Resource.Id.text);
                if (i == pos)
                    text.SetTypeface(text.Typeface, TypefaceStyle.Bold);
                else
                    text.SetTypeface(text.Typeface, TypefaceStyle.Normal);

            }
        }

        protected override void OnPostCreate(Bundle savedInstanceState)
        {
            base.OnPostCreate(savedInstanceState);
            drawerToggle.SyncState();
        }

        public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            drawerToggle.OnConfigurationChanged(newConfig);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (drawerToggle.OnOptionsItemSelected(item))
                return true;
            return base.OnOptionsItemSelected(item);
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            try
            {
                if (mapFragment != null && mapFragment.IsVisible)
                    mapFragment.OnSearchIntent(intent);
            }
            catch
            {
            }
        }

        protected override void OnStart()
        {
            if (client != null)
                client.Connect();
            base.OnStart();
        }

        protected override void OnStop()
        {
            if (client != null)
                client.Disconnect();
            base.OnStop();
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == ConnectionFailureResolutionRequest)
            {
                if (resultCode == Result.Ok && CheckGooglePlayServices())
                {
                    if (client == null)
                    {
                        client = CreateApiClient();
                        client.Connect();
                    }
                    SwitchTo(mapFragment = new ProntoMapFragment(this));
                }
                else
                    Finish();
            }
            else
            {
                base.OnActivityResult(requestCode, resultCode, data);
            }
        }

        bool CheckGooglePlayServices()
        {
            var result = GooglePlayServicesUtil.IsGooglePlayServicesAvailable(this);
            if (result == ConnectionResult.Success)
                return true;
            var dialog = GooglePlayServicesUtil.GetErrorDialog(result,
                    this,
                    ConnectionFailureResolutionRequest);
            if (dialog != null)
            {
                var errorDialog = new ErrorDialogFragment { Dialog = dialog };
                errorDialog.Show(SupportFragmentManager, "Google Services Updates");
                return false;
            }

            Finish();
            return false;
        }

        #region IObserver implementation

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Station[] value)
        {
            if (client == null || !client.IsConnected)
                return;
            var location = LocationServices.FusedLocationApi.GetLastLocation(client);
            if (location == null)
                return;
            var stations = Pronto.GetStationsAround(value,
                      new GeoPoint { Lat = location.Latitude, Lon = location.Longitude },
                      minDistance: 1,
                      maxItems: 4);
            RunOnUiThread(() => aroundAdapter.SetStations(stations));
        }

        #endregion

        public void OnConnected(Bundle p0)
        {
            if (Pronto.Instance.LastStations != null)
                OnNext(Pronto.Instance.LastStations);
        }

        public void OnDisconnected()
        {
			
        }

        public void OnConnectionFailed(ConnectionResult p0)
        {
			
        }

        public void OnConnectionSuspended(int reason)
        {

        }
    }


    class MyViewHolder : Java.Lang.Object
    {
        public TextView Title { get; set; }
    }

    class DrawerMenuAdapter : BaseAdapter
    {
        Tuple<int, string>[] sections = {
            Tuple.Create(Resource.Drawable.ic_drawer_map, "Map"),
            Tuple.Create(Resource.Drawable.ic_drawer_star, "Favorites"),
            Tuple.Create(Resource.Drawable.ic_about, "About"),
        };

        Context context;

        public DrawerMenuAdapter(Context context)
        {
            this.context = context;
        }

        public override Java.Lang.Object GetItem(int position)
        {
            return new Java.Lang.String(sections[position - 1].Item2);
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            MyViewHolder holder = null;

            if (view != null)
                holder = view.Tag as MyViewHolder;

            if (holder == null)
            {
                holder = new MyViewHolder();
                var inflater = context.GetSystemService(Context.LayoutInflaterService).JavaCast<LayoutInflater>();
                view = inflater.Inflate(Resource.Layout.DrawerItemLayout, parent, false);
                holder.Title = view.FindViewById<TextView>(Resource.Id.text);
                view.Tag = holder;

            }

            if (position == 0 && convertView == null)
                holder.Title.SetTypeface(holder.Title.Typeface, TypefaceStyle.Bold);
            else
                holder.Title.SetTypeface(holder.Title.Typeface, TypefaceStyle.Normal);


            holder.Title.Text = sections[position].Item2;
            holder.Title.SetCompoundDrawablesWithIntrinsicBounds(sections[position].Item1, 0, 0, 0);

            return view;
        }

        public override int Count
        {
            get
            {
                return sections.Length;
            }
        }

        public override bool IsEnabled(int position)
        {
            return true;
        }

        public override bool AreAllItemsEnabled()
        {
            return false;
        }
    }

    class DrawerAroundAdapter : BaseAdapter
    {
        Context context;

        Station[] stations = null;
        FavoriteManager manager;
        HashSet<int> favorites;

        Drawable starDrawable;

        public DrawerAroundAdapter(Context context)
        {
            this.context = context;
            this.manager = FavoriteManager.Obtain(context);
            this.starDrawable = XamSvg.SvgFactory.GetDrawable(context.Resources, Resource.Raw.star_depressed);
            this.favorites = new HashSet<Int32>();
            LoadFavorites();
        }

        public void SetStations(Station[] stations)
        {
            this.stations = stations;
            NotifyDataSetChanged();
        }

        async void LoadFavorites()
        {
            if (manager.LastFavorites != null)
                favorites = manager.LastFavorites;
            else
                favorites = await manager.GetFavoriteStationIdsAsync();
            NotifyDataSetChanged();
        }

        public void Refresh()
        {
            LoadFavorites();
        }

        public override Java.Lang.Object GetItem(int position)
        {
            return new Java.Lang.String(stations[position].Name);
        }

        public override long GetItemId(int position)
        {
            return stations[position].Id;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            if (view == null)
            {
                var inflater = context.GetSystemService(Context.LayoutInflaterService).JavaCast<LayoutInflater>();
                view = inflater.Inflate(Resource.Layout.DrawerAroundItem, parent, false);
            }

            var star = view.FindViewById<ImageView>(Resource.Id.aroundStar);
            var stationName = view.FindViewById<TextView>(Resource.Id.aroundStation1);
            var stationNameSecond = view.FindViewById<TextView>(Resource.Id.aroundStation2);
            var bikes = view.FindViewById<TextView>(Resource.Id.aroundBikes);
            var racks = view.FindViewById<TextView>(Resource.Id.aroundRacks);

            var station = stations[position];
            star.SetImageDrawable(starDrawable);
            star.Visibility = favorites.Contains(station.Id) ? ViewStates.Visible : ViewStates.Invisible;

            stationName.Text = station.Street;//StationUtils.CutStationName (station.Name, out secondPart);
            stationNameSecond.Text = station.Name;
            bikes.Text = station.BikeCount.ToString();
            racks.Text = station.EmptySlotCount.ToString();

            return view;
        }

        public override int Count
        {
            get
            {
                return stations == null ? 0 : stations.Length;
            }
        }

        public override bool IsEnabled(int position)
        {
            return true;
        }

        public override bool AreAllItemsEnabled()
        {
            return false;
        }
    }

    class ErrorDialogFragment : Android.Support.V4.App.DialogFragment
    {
        public new Dialog Dialog
        {
            get;
            set;
        }

        public override Dialog OnCreateDialog(Bundle savedInstanceState)
        {
            return Dialog;
        }
    }

    class ProntoActionBarToggle : Android.Support.V7.App.ActionBarDrawerToggle
    {
        public ProntoActionBarToggle(Activity activity,
                                DrawerLayout drawer,
                                int openDrawerContentRes,
                                int closeDrawerContentRest)
            : base(activity, drawer, openDrawerContentRes, closeDrawerContentRest)
        {
        }

        public Action OpenCallback
        {
            get;
            set;
        }

        public Action CloseCallback
        {
            get;
            set;
        }

        public override void OnDrawerOpened(View drawerView)
        {
            base.OnDrawerOpened(drawerView);
            if (OpenCallback != null)
                OpenCallback();
        }

        public override void OnDrawerClosed(View drawerView)
        {
            base.OnDrawerClosed(drawerView);
            if (CloseCallback != null)
                CloseCallback();
        }
    }
}


