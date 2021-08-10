const AWS = require('aws-sdk');
const lambda = new AWS.Lambda();

exports.handler = (event) => {
    
    if (event.Records[0].s3.bucket.name == process.env.s3Bucket && event.Records[0].s3.object.key == process.env.s3Key)
    {
        var params = {
            FunctionName: process.env.functionToUpdate,
            S3Bucket: event.Records[0].s3.bucket.name, 
            S3Key: event.Records[0].s3.object.key
        };
        
        // https://docs.aws.amazon.com/AWSJavaScriptSDK/latest/AWS/Lambda.html#updateFunctionCode-property
        lambda.updateFunctionCode(params, function(err, data) {
            if (err) // an error occurred
            {
                console.log(err, err.stack);
            }
            else
            {   
                console.log(data);  
            }
        });
    }
    else
    {
        console.log("bucket name or s3 key did not match expected values.");
        console.log("expected bucket name: " + process.env.s3Bucket + " actual: " + event.Records[0].s3.bucket.name);
        console.log("expected s3 key: " + process.env.s3Key + " actual: " + event.Records[0].s3.object.key);
    }
    console.log("Exiting");
};
