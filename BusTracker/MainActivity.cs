using Android.App;
using Android.OS;
using System;
using Android.Widget;
using Android.Content;

namespace BusTracker
{
    [Activity(Label = "MainActivity", MainLauncher = true, Theme = "@android:style/Theme.Holo.NoActionBar.Fullscreen")]   
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource          
            SetContentView(Resource.Layout.Main);            
            Init();  
        }

        //Initialize the button used to open the map
        private void Init()
        {
            Button mbutton = (Button)FindViewById(Resource.Id.button);
            mbutton.Click += Button_Click;        
        }

        //Create the button click event
        private void Button_Click(object sender, EventArgs e)
        {
            Intent intent = new Intent(this, typeof(MapActivity));
            StartActivity(intent);
        }       
    }
}

