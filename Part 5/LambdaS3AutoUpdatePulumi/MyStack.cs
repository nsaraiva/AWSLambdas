using Pulumi;
using S3 = Pulumi.Aws.S3;
using Aws = Pulumi.Aws;

class MyStack : Stack
{
    public MyStack()
    {
        string resource_prefix = "PulumiHelloWorldAutoUpdate";

        var lambdaHelloWorldRole = new Aws.Iam.Role($"{resource_prefix}_LambdaRole", new Aws.Iam.RoleArgs
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

        var lambdaUpdateRole = new Aws.Iam.Role($"{resource_prefix}_LambdaUpdateRole", new Aws.Iam.RoleArgs
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

        // gives the lamaba permissions to other lambdas and s3 - too many permissions, but this is a demo.
        var lambdaUpdatePolicy = new Aws.Iam.Policy($"{resource_prefix}_S3_Lambda_Policy", new Aws.Iam.PolicyArgs{
            PolicyDocument = 
@"{
    ""Version"": ""2012-10-17"",
    ""Statement"": [
        {
            ""Sid"": """",
            ""Effect"": ""Allow"",
            ""Action"": [
                ""s3:*"",
                ""logs:*"",
                ""lambda:*""
            ],
            ""Resource"": ""*""
        }
    ]
}"
        });

        // attach a simple policy to the hello world lambda.
        var lambdaHelloWorldAttachment = new Aws.Iam.PolicyAttachment($"{resource_prefix}_LambdaHelloWorldPolicyAttachment", new Aws.Iam.PolicyAttachmentArgs
        {
            Roles =
            {
                lambdaHelloWorldRole.Name
            },
            PolicyArn = Aws.Iam.ManagedPolicy.AWSLambdaBasicExecutionRole.ToString(),
        });

        // attach the custom policy to the role that runs the update lambda.
        var lambdaUpdateAttachment = new Aws.Iam.PolicyAttachment($"{resource_prefix}_LambdaUpdatePolicyAttachment", new Aws.Iam.PolicyAttachmentArgs
        {
            Roles =
            {
                lambdaUpdateRole.Name
            },
            PolicyArn = lambdaUpdatePolicy.Arn,
        });

        var s3Bucket = new S3.Bucket($"{resource_prefix}_S3Bucket", new S3.BucketArgs
        {
            BucketName = "pulumi-hello-world-auto-update-s3-bucket",
            Versioning = new Aws.S3.Inputs.BucketVersioningArgs
            {
                Enabled = true,
            },
            Acl = "private"
        });

        var s3BucketObject = new S3.BucketObject($"{resource_prefix}_ZipFile", new S3.BucketObjectArgs
        {
            Bucket = s3Bucket.BucketName.Apply(name => name),
            Acl = "private",
            Source = new FileArchive("./Lambdas/helloworld_no_date/helloworld.zip"),
            Key = "helloworld.zip"
        });

        // this is the lambda that runs .NET code
        var lambdaHelloWorldFunction = new Aws.Lambda.Function($"{resource_prefix}_LambdaHelloWorldFunction", new Aws.Lambda.FunctionArgs
        {
            Handler = "HelloWorldLambda::HelloWorldLambda.Function::FunctionHandler",
            MemorySize = 128,
            Publish = false,
            ReservedConcurrentExecutions = -1,
            Role = lambdaHelloWorldRole.Arn,
            Runtime = Aws.Lambda.Runtime.DotnetCore3d1,
            Timeout = 4,
            S3Bucket = s3Bucket.BucketName,
            S3Key = s3BucketObject.Key
        });

        // this is the lambda triggered by an upload to S3 and replaces the zip in the above lambda
        var lambdaUpdateFunction = new Aws.Lambda.Function($"{resource_prefix}_LambdaUpdateFunction", new Aws.Lambda.FunctionArgs
        {
            Handler = "index.handler",
            MemorySize = 128,
            Publish = false,
            ReservedConcurrentExecutions = -1,
            Role = lambdaUpdateRole.Arn,
            Runtime = Aws.Lambda.Runtime.NodeJS14dX,
            Timeout = 4,
            Code = new FileArchive("./Lambdas/LambdaUpdater/index.zip"),
            Environment = new Aws.Lambda.Inputs.FunctionEnvironmentArgs
            {
                Variables = new InputMap<string> {{"s3Bucket", s3Bucket.BucketName}, {"s3Key", "helloworld.zip"}, {"functionToUpdate", lambdaHelloWorldFunction.Name}}
            }
        });

        var s3BucketPermissionToCallLambda = new Aws.Lambda.Permission($"{resource_prefix}_S3BucketPermissionToCallLambda", new Aws.Lambda.PermissionArgs
        {
            Action = "lambda:InvokeFunction",
            Function = lambdaUpdateFunction.Arn,
            Principal = "s3.amazonaws.com",
            SourceArn = s3Bucket.Arn,
        });

        var bucketNotification = new S3.BucketNotification($"{resource_prefix}_S3BucketNotification", new Aws.S3.BucketNotificationArgs
        {
            Bucket = s3Bucket.Id,
            LambdaFunctions = 
            {
                new Aws.S3.Inputs.BucketNotificationLambdaFunctionArgs
                {
                    LambdaFunctionArn = lambdaUpdateFunction.Arn,
                    Events = 
                    {
                        "s3:ObjectCreated:*",
                    },
                }
            },
        }, new CustomResourceOptions
        {
            DependsOn = 
            {
                s3BucketPermissionToCallLambda,
            },
        });

        // keep the contents bucket private
        var bucketPublicAccessBlock = new S3.BucketPublicAccessBlock($"{resource_prefix}_PublicAccessBlock", new S3.BucketPublicAccessBlockArgs
        {
            Bucket = s3Bucket.Id,
            BlockPublicAcls = false,  // leaving these two false because I need them this way 
            IgnorePublicAcls = false, // for a post about GitHub Actions that I'm working on
            BlockPublicPolicy = true,
            RestrictPublicBuckets = true
        });

        this.LambdaUpdateFunctionName = lambdaUpdateFunction.Name;
        this.LambdaHelloWorldFunctionName = lambdaHelloWorldFunction.Name;
        this.S3Bucket = s3Bucket.BucketName;
        this.S3Key = s3BucketObject.Key;
    }

    [Output]
    public Output<string> LambdaUpdateFunctionName { get; set; }

    [Output]
    public Output<string> LambdaHelloWorldFunctionName { get; set; }

    [Output]
    public Output<string> S3Bucket {get;set;}

    [Output]
    public Output<string> S3Key {get;set;}
}
