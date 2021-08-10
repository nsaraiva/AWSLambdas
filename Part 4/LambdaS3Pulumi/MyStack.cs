using Pulumi;
using S3 = Pulumi.Aws.S3;
using Aws = Pulumi.Aws;
class MyStack : Stack
{
    public MyStack()
    {

        var lambdaRole = new Aws.Iam.Role("PulumiHelloWorld_LambdaRole", new Aws.Iam.RoleArgs
        {
            AssumeRolePolicy =
@"{
    ""Version"": ""2012-10-17"",
    ""Statement"": [
        {
        ""Action"": ""sts:AssumeRole"",
        ""Principal"": {
            ""Service"": ""lambda.amazonaws.com""
        },
        ""Effect"": ""Allow"",
        ""Sid"": """"
        }
    ]
}",
        });

        var lambdaPolicyAttachment = new Aws.Iam.PolicyAttachment("PulumiHelloWorld_LambdaPolicyAttachment", new Aws.Iam.PolicyAttachmentArgs
        {
            Roles =
            {
                lambdaRole.Name
            },
            PolicyArn = Aws.Iam.ManagedPolicy.AWSLambdaBasicExecutionRole.ToString(),
        });

        var bucket = new S3.Bucket("PulumiHelloWorld_S3Bucket", new S3.BucketArgs
        {
            BucketName = "pulumi-hello-world-s3-bucket",
            Acl = "private"
        });

        var bucketPublicAccessBlock = new S3.BucketPublicAccessBlock("PulumiHelloWorld_PublicAccessBlock", new S3.BucketPublicAccessBlockArgs
        {
            Bucket = bucket.Id,
            BlockPublicAcls = true,
            BlockPublicPolicy = true,
            RestrictPublicBuckets = true,
            IgnorePublicAcls = true

        });

        var bucketObject = new S3.BucketObject("PulumiHelloWorld_ZipFile", new S3.BucketObjectArgs
        {
            Bucket = bucket.BucketName.Apply(name => name),
            Acl = "private",
            Source = new FileArchive("helloworld.zip")
        });

        var lambdaFunction = new Aws.Lambda.Function("PulumiHelloWorld_LambdaFunction", new Aws.Lambda.FunctionArgs
        {
            Handler = "HelloWorldLambda::HelloWorldLambda.Function::FunctionHandler",
            MemorySize = 128,
            Publish = false,
            ReservedConcurrentExecutions = -1,
            Role = lambdaRole.Arn,
            Runtime = Aws.Lambda.Runtime.DotnetCore3d1,
            Timeout = 4,
            S3Bucket = bucket.BucketName,
            S3Key = bucketObject.Key
        });

        this.LambdaFunctionName = lambdaFunction.Name;
    }

    [Output]
    public Output<string> LambdaFunctionName { get; set; }
}
