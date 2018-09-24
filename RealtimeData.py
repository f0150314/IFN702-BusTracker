from google.transit import gtfs_realtime_pb2
import paho.mqtt.client as mqtt
import requests
import time as t

# Server connection
broker_address = "203.101.226.126"
broker_port = "1883"
client = mqtt.Client("P1")  # create new instance
client.connect(broker_address)  # connect to broker

feed = gtfs_realtime_pb2.FeedMessage()
while True:
    response = requests.get('https://gtfsrt.api.translink.com.au/Feed/SEQ')
    feed.ParseFromString(response.content)
    for entity in feed.entity:                
        if (entity.vehicle.trip.route_id == '60-1118') \
        or (entity.vehicle.trip.route_id == '60-1163'):
            
            print('---------------------60---------------------------')
            print('bus ID: ', entity.vehicle.vehicle.id)
            print('bus lat: ', entity.vehicle.position.latitude)
            print('bus lng: ', entity.vehicle.position.longitude)
            print()            
            topic = "Bus/60/" + entity.vehicle.vehicle.id
            message = entity.vehicle.vehicle.id + ',' + \
            str(entity.vehicle.position.latitude) + ',' + \
            str(entity.vehicle.position.longitude)
            client.publish(topic, message)
                
        if (entity.vehicle.trip.route_id == '196-1118') \
        or (entity.vehicle.trip.route_id == '196-1163'):           
            print('---------------------196---------------------------')
            print('bus ID: ', entity.vehicle.vehicle.id)
            print('bus lat: ', entity.vehicle.position.latitude)
            print('bus lng: ', entity.vehicle.position.longitude)
            print()           
            topic = "Bus/196/" + entity.vehicle.vehicle.id
            message = entity.vehicle.vehicle.id + ',' + \
            str(entity.vehicle.position.latitude) + ',' + \
            str(entity.vehicle.position.longitude)
            client.publish(topic, message)
              
        if (entity.vehicle.trip.route_id == '340-1118') \
        or (entity.vehicle.trip.route_id == '340-1163'):
            print('---------------------340---------------------------')
            print('bus ID: ', entity.vehicle.vehicle.id)
            print('bus lat: ', entity.vehicle.position.latitude)
            print('bus lng: ', entity.vehicle.position.longitude)
            print()            
            topic = "Bus/340/" + entity.vehicle.vehicle.id
            message = entity.vehicle.vehicle.id + ',' + \
            str(entity.vehicle.position.latitude) + ',' + \
            str(entity.vehicle.position.longitude)
            client.publish(topic, message)
                    
    t.sleep(8)
    