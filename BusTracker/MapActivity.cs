﻿using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Android.OS;
using Android.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Gms.Location;
using Android.Locations;
using Android.Support.V4.App;
using Android.Views.InputMethods;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Linq;

namespace BusTracker
{
    [Activity(Label = "MapActivity", Theme = "@android:style/Theme.Holo.NoActionBar")]
    public class MapActivity : Activity, IOnMapReadyCallback
    {
        GoogleMap mMap;
        ImageView mGps;
        Marker busMarker;
        ArrayAdapter autoCompleteAdapter;
        AutoCompleteTextView autoCompleteTextView;
        FusedLocationProviderClient fusedLocationProviderClient;

        // Location instance for stroing device location
        Location deviceLocation = new Location("deviceLocation");

        // Create MQTT client instance
        MqttClient client = new MqttClient(broker_address);

        // Create dictionary to store markers for later removal
        Dictionary<string, Marker> markerDepository = new Dictionary<string, Marker>();

        // Create dictionary to store bus shape 
        Dictionary<string, List<List<double>>> busShape = new Dictionary<string, List<List<double>>>();

        // Global variables
        private float DEFAULT_ZOOM = 15f;
        private string currentSub = null;
        private static string broker_address = "203.101.226.126";
        private string[] autoCompleteOptions;   

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Create your application here
            SetContentView(Resource.Layout.MapActivity);

            //Create a map
            SetUpMap();                 
        }

        // Initialize the map
        private void SetUpMap()
        {
            MapFragment mapFragment = (MapFragment)FragmentManager.FindFragmentById(Resource.Id.map);

            // Call GetMapAsync Method for map deployment in this activity 
            mapFragment.GetMapAsync(this);
        }

        // Override the OnMapReady method to make use of Google map services
        public void OnMapReady(GoogleMap googleMap)
        {
            // Instantiate google map instance
            mMap = googleMap;
            GetDeviceLocation();

            // Inbult UI features
            mMap.MyLocationEnabled = true;
            mMap.UiSettings.ZoomControlsEnabled = true;
            mMap.UiSettings.ZoomGesturesEnabled = true;

            // Initialize GPS icon
            mGps = FindViewById<ImageView>(Resource.Id.gps);
            mGps.Click += GetSelfLocation;

            // Pre-process shapes file for later ployline deployment
            BusShapeCollection();

            // Initialize AutoCompleteAdapter and AutocompleteTextView
            autoCompleteOptions = CollectBusOptions();
            autoCompleteAdapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleDropDownItem1Line, autoCompleteOptions);
            autoCompleteTextView = (AutoCompleteTextView)FindViewById(Resource.Id.input_search);
            autoCompleteTextView.Adapter = autoCompleteAdapter;
            autoCompleteTextView.EditorAction += AutoCompleteTextView_EditorAction;
           
        }

        // Get current location
        private async void GetDeviceLocation()
        {
            // Instantiate the location provider to obtain device location
            fusedLocationProviderClient = LocationServices.GetFusedLocationProviderClient(this);
            Location location = await fusedLocationProviderClient.GetLastLocationAsync();
            deviceLocation = location;

            double lng;
            double lat;

            // Produce error message if no location info is detected
            try
            {
                lng = location.Longitude;
                lat = location.Latitude;
                moveCamera(new LatLng(lat, lng), DEFAULT_ZOOM);
            }
            catch (Exception e)
            {
                Log.Error("MapActvity", "Exception: " + e);
            }
        }

        // Moving camera
        private void moveCamera(LatLng latLng, float zoom)
        {
            mMap.MoveCamera(CameraUpdateFactory.NewLatLngZoom(latLng, zoom));
        }

        // Get device location when click on the GPS button
        private void GetSelfLocation(object sender, EventArgs e)
        {
            GetDeviceLocation();
        }

        // Retrieve Translink bus route info from routes.txt
        private string[] CollectBusOptions()
        {
            Stream seedDataStream = Assets.Open("routes.txt");
            List<string> busRoute = new List<string>();
            using (StreamReader reader = new StreamReader(seedDataStream))
            {
                string line;
                bool flag = true;
                while ((line = reader.ReadLine()) != null)
                {
                    // Prevent the first line of irrelevant text being stored
                    if (flag)
                        flag = false;
                    // Processing and store only needed bus route number
                    else
                    {
                        string[] lineToken = line.Split('-');
                        // Set condition to prevent storing repeated info
                        if (!busRoute.Contains(lineToken[0]))
                            busRoute.Add(lineToken[0]);
                    }
                }
            }
            string[] busOptions = busRoute.ToArray();
            return busOptions;
        }

        // Allow keyboard to perform action after receiving the input
        private void AutoCompleteTextView_EditorAction(object sender, TextView.EditorActionEventArgs e)
        {
            e.Handled = false;
            if (e.ActionId == ImeAction.Search || e.ActionId == ImeAction.Done 
                || e.Event.Action == KeyEventActions.Down && e.Event.KeyCode == Keycode.Enter)
            {               
                HideKeyboard();

                // Once the input is filtered, SetUpServerConnection method is called in inside this method 
                InputFiltering();
                e.Handled = true; 
            }
        }

        // Hide keyboard
        private void HideKeyboard()
        {
            InputMethodManager inputManager = (InputMethodManager)GetSystemService(InputMethodService);
            inputManager.HideSoftInputFromWindow(CurrentFocus.WindowToken, HideSoftInputFlags.None);
        }

        // Establish server connection if any available bus is selected
        private void InputFiltering()
        {
            string busNum = autoCompleteTextView.Text;

            // Identify if the input is correct 
            if (Array.Exists(autoCompleteOptions, option => option == busNum))
            { 
                Toast.MakeText(this, "Bus ID = " + busNum, ToastLength.Short).Show();
                SetUpServerConnection(busNum);               
            }
            else
                Toast.MakeText(this, "No bus was found", ToastLength.Short).Show();
        }
 
        // Create server connection or modify existing subscription
        private void SetUpServerConnection(string busNum)                           
        {
            // Check server connection
            if (!client.IsConnected)
            {
                // register to message received
                client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
                string clientId = Guid.NewGuid().ToString();
                client.Connect(clientId);

                DrawPolyline(busShape, busNum);

                // subscribe to the topic "Bus/busNum/#"
                client.Subscribe(new string[] { $"Bus/{busNum}/#" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                currentSub = busNum;
            }
            else // re-subscribe to new topic
            {
                if (busNum != currentSub)
                {
                    client.Unsubscribe(new string[] { $"Bus/{currentSub}/#" });

                    // clear all markers that belongs to previous topic on the map and clear the dictionary that stores the markers of previous topic.
                    mMap.Clear();
                    markerDepository.Clear();

                    DrawPolyline(busShape, busNum);
                    client.Subscribe(new string[] { $"Bus/{busNum}/#" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                    currentSub = busNum;
                }
            }
        }

        // Receive the published messages and print them out
        private void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            // Get message info
            string busInfo = Encoding.UTF8.GetString(e.Message);
            Console.WriteLine(e.Topic);
            Console.WriteLine(busInfo);

            // Allow addMarker function to run on main thread
            RunOnUiThread(() => BusVisualization(e, busInfo));            
        }

        private void BusVisualization(MqttMsgPublishEventArgs e, string busInfo)
        {
            // Pre-process MQTT messages
            string[] busInfoTokens = busInfo.Split(',');
            string topic = busInfoTokens[0];
            double busLat = Convert.ToDouble(busInfoTokens[1]);
            double busLng = Convert.ToDouble(busInfoTokens[2]);

            // Calculate the distance between current location and buses
            Location busLocation = new Location("bus location");
            busLocation.Latitude = busLat;
            busLocation.Longitude = busLng;
            int distance = (int)deviceLocation.DistanceTo(busLocation);

            // Periodically update buses' position by adding new and remo
            // Check whether the markers have already existed or not
            if (!markerDepository.ContainsKey(e.Topic))
            {
                // Add to the map
                busMarker = mMap.AddMarker(new MarkerOptions()
                                .SetTitle(e.Topic + ", Distance: " + distance + "m")
                                .SetIcon(BitmapDescriptorFactory.FromResource(Resource.Drawable.bus))
                                .SetPosition(new LatLng(busLat, busLng)));
                
                //Add to the dictionary 
                markerDepository.Add(e.Topic, busMarker);
            }

            // 1. Remove the previous markers from the map and dictionary if markers have already existed on the map and in dictionary
            // 2. Add the new markers and store them to dictionary
            else
            {
                busMarker = markerDepository[e.Topic];
                busMarker.Remove();
                markerDepository.Remove(e.Topic);
                busMarker = mMap.AddMarker(new MarkerOptions()
                                .SetTitle(e.Topic + ", Distance: " + distance + "m")
                                .SetIcon(BitmapDescriptorFactory.FromResource(Resource.Drawable.bus))
                                .SetPosition(new LatLng(busLat, busLng)));
                markerDepository.Add(e.Topic, busMarker);
            }
        }

        // Preprocess the shapes.txt file to get actual bus path
        private void BusShapeCollection()
        {            
            List<List<double>> shapeCoordinates = new List<List<double>>();
            Stream seedDataStream = Assets.Open("shapes.txt");           
            
            // Read the shape.txt file
            using (StreamReader reader = new StreamReader(seedDataStream))
            {
                string line;
                bool flag = true;
                string shape_id = null;

                while ((line = reader.ReadLine()) != null)
                {
                    // Prevent the first line of irrelevant text being stored
                    if (flag)
                        flag = false;
                    // Processing and store only needed bus route number
                    else
                    {                      
                        string[] lineToken = line.Split(',');
                        
                        if (lineToken[0] == shape_id)
                        {
                            shapeCoordinates[0].Add(Convert.ToDouble(lineToken[1]));
                            shapeCoordinates[1].Add(Convert.ToDouble(lineToken[2]));
                        }
                        else
                        {
                            // prevent storing null shape_id into dictionary
                            if (shape_id != null)
                            {
                                // Create new object for storing, otherwise the data will be removed when using .Clear() method
                                List<List<double>> shapeCoordinateList = new List<List<double>>(shapeCoordinates);
                                busShape.Add(shape_id, shapeCoordinateList);
                                shapeCoordinates.Clear();
                            }

                            // A proper way to create 2D list
                            shapeCoordinates.Add(new List<double>());
                            shapeCoordinates[0].Add(Convert.ToDouble(lineToken[1]));
                            shapeCoordinates.Add(new List<double>());
                            shapeCoordinates[1].Add(Convert.ToDouble(lineToken[2]));
                            shape_id = lineToken[0];
                        }                      
                    }
                }
            }
        }

        // Draw bus route
        private void DrawPolyline(Dictionary<string, List<List<double>>> busShape, string busNum)
        {
            // Acquire needed shape_id
            var keys = busShape.Keys.Where(key => key.StartsWith(busNum) && key.Length == busNum.Length + 4).ToArray();

            // Add polylines for every shape_id
            foreach (var key in keys)
            {
                List<List<double>> busLatLng = busShape[key];
                PolylineOptions polyOptions = new PolylineOptions();
                    
                for (int i = 0; i < busLatLng[0].Count; i++)
                {
                    polyOptions.Add(new LatLng(busLatLng[0][i], busLatLng[1][i]));
                }
                mMap.AddPolyline(polyOptions);
            }
        }
    }
}