using Pulumi;
using Aws = Pulumi.Aws;

class MyStack : Stack
{
    public MyStack()
    {

        var lambdaRole = new Aws.Iam.Role("PulumiWebApiGateway_LambdaRole", new Aws.Iam.RoleArgs
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

        var lambdaPolicyAttachment = new Aws.Iam.PolicyAttachment("PulumiWebApiGateway_LambdaPolicyAttachment", new Aws.Iam.PolicyAttachmentArgs
        {
            Roles =
            {
                lambdaRole.Name
            },
            PolicyArn = Aws.Iam.ManagedPolicy.AWSLambdaBasicExecutionRole.ToString(), 
        });

        var lambdaFunction = new Aws.Lambda.Function("PulumiWebApiGateway_LambdaFunction", new Aws.Lambda.FunctionArgs
        {
            Handler = "WebAPILambda::WebAPILambda.LambdaEntryPoint::FunctionHandlerAsync",
            MemorySize = 128,
            Publish = false,
            ReservedConcurrentExecutions = -1,
            Role = lambdaRole.Arn,
            Runtime = Aws.Lambda.Runtime.DotnetCore3d1,
            Timeout = 4,
            Code = new FileArchive("WebAPILambda.zip"),
        });
        System.Console.WriteLine(Aws.Iam.ManagedPolicy.AWSLambdaBasicExecutionRole.ToString());

        var httpApiGateway = new Pulumi.Aws.ApiGatewayV2.Api("PulumiWebApiGateway_ApiGateway", new Pulumi.Aws.ApiGatewayV2.ApiArgs
        {
            ProtocolType = "HTTP",
            RouteSelectionExpression = "${request.method} ${request.path}",
        });

        var httpApiGateway_LambdaIntegration = new Pulumi.Aws.ApiGatewayV2.Integration("PulumiWebApiGateway_ApiGatewayIntegration", new Pulumi.Aws.ApiGatewayV2.IntegrationArgs
        {
            ApiId = httpApiGateway.Id,
            IntegrationType = "AWS_PROXY",
            IntegrationMethod = "POST",
            IntegrationUri = lambdaFunction.Arn,
            PayloadFormatVersion = "2.0",
            TimeoutMilliseconds = 30000,
        });

        var httpApiGatewayRoute = new Pulumi.Aws.ApiGatewayV2.Route("PulumiWebApiGateway_ApiGatewayRoute", new Pulumi.Aws.ApiGatewayV2.RouteArgs
        {
            ApiId = httpApiGateway.Id,
            RouteKey = "$default",
            Target = httpApiGateway_LambdaIntegration.Id.Apply(id => $"integrations/{id}"),
        });

        var httpApiGatewayStage = new Pulumi.Aws.ApiGatewayV2.Stage("PulumiWebApiGateway_ApiGatewayStage", new Pulumi.Aws.ApiGatewayV2.StageArgs
        {
            ApiId = httpApiGateway.Id,
            AutoDeploy = true,
            Name = "$default",
        });

        var lambdaPermissionsForApiGateway = new Aws.Lambda.Permission("PulumiWebApiGateway_LambdaPermission", new Aws.Lambda.PermissionArgs
        {
            Action = "lambda:InvokeFunction",
            Function = lambdaFunction.Name,
            Principal = "apigateway.amazonaws.com",
            SourceArn = Output.Format($"{httpApiGateway.ExecutionArn}/*") // note it's the ExecutionArn.
            // SourceArn = httpApiGateway.ExecutionArn.Apply(arn => $"{arn}/*") // this is another way of doing the same thing
        });

        this.ApiEndpoint = httpApiGateway.ApiEndpoint.Apply(endpoint =>  $"{endpoint}/api/values");
    }

    [Output]
    public Output<string> ApiEndpoint { get; set; }
}