from google.transit import gtfs_realtime_pb2
import paho.mqtt.client as mqtt
import requests
import time as t

# Server connection
broker_address = "203.101.226.126"
broker_port = "1883"
client = mqtt.Client("P1")  # create new instance
client.connect(broker_address)  # connect to broker

# Extracting the GTFS-RT feed from translink services
feed = gtfs_realtime_pb2.FeedMessage()
while True:
    response = requests.get('https://gtfsrt.api.translink.com.au/Feed/SEQ')
    feed.ParseFromString(response.content)
    for entity in feed.entity:
        
        # Preprocess the feed to acqure bus route id
        busRoute = entity.vehicle.trip.route_id.split('-')[0]                
            
        # Show the output of ID, latitude and longitude
        print('---------------------{}---------------------------'.format(busRoute))
        print('bus ID: ', entity.vehicle.vehicle.id)
        print('bus lat: ', entity.vehicle.position.latitude)
        print('bus lng: ', entity.vehicle.position.longitude)
        print() 
        
        # Create topic and message for publishing message
        topic = "Bus/{}/{}".format(busRoute, entity.vehicle.vehicle.id) 
        message = entity.vehicle.vehicle.id + ',' + \
        str(entity.vehicle.position.latitude) + ',' + \
        str(entity.vehicle.position.longitude)
        client.publish(topic, message)       
    
    # Updating the data every 5 seconds                
    t.sleep(5)