import json
import boto3
import random

def lambda_handler(event, context):
    dynamodb = boto3.resource('dynamodb')
    table = dynamodb.Table('Players')
    
    randID = random.randint(1,10)
    
    response = table.get_item(
        Key={
            'user_id' : "Player" + str(randID)
        }
    )
    
    item = response['Item']
    
    return {
        'statusCode': 200,
        'body': json.dumps(item)
    }