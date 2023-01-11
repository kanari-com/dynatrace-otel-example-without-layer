using Amazon.Lambda.Core;
using Dynatrace.OpenTelemetry;
using Dynatrace.OpenTelemetry.Instrumentation.AwsLambda;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace MySimpleFunction
{

    public class DisplayNewUser
    {
        private static TracerProvider? tracerProvider;
        private string GetSecret()
        {
            string secretName = "DT_CONNECTION_AUTH_TOKEN";
            string region = "us-east-1";
            IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
            GetSecretValueRequest request = new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
            };
            GetSecretValueResponse response = client.GetSecretValueAsync(request).GetAwaiter().GetResult();
            return response.SecretString;
        }

        public Task FunctionHandlerAsync(NewUser input, ILambdaContext context)
        {
            var secret = GetSecret();
            DynatraceSetup.InitializeLogging();
            var dtcluster = Environment.GetEnvironmentVariable("DT_CONNECTION_BASE_URL");
            Environment.SetEnvironmentVariable("DT_CONNECTION_AUTH_TOKEN", secret); //Get this from a secret storage
            tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddDynatrace()
                // Configures AWS Lambda invocations tracing
                .AddAWSLambdaConfigurations(c => c.DisableAwsXRayContextExtraction = true)
                // Instrumentation for creation of span (Activity) representing AWS SDK call.
                // Can be omitted if there are no outgoing AWS SDK calls to other AWS Lambdas and/or calls to AWS services like DynamoDB and SQS.
                //.AddAWSInstrumentation(c => c.SuppressDownstreamInstrumentation = true)
                // Adds injection of Dynatrace-specific context information in certain SDK calls (e.g. Lambda Invoke).
                // Can be omitted if there are no outgoing calls to other Lambdas via the AWS Lambda SDK.
                .AddDynatraceAwsSdkInjection()
                .Build();
            LambdaLogger.Log($"Initializing Setup\n");
            LambdaLogger.Log($"{secret}");
            var propagationContext = AwsLambdaHelpers.ExtractPropagationContext(context);
            LambdaLogger.Log($"Calling function name: {context.FunctionName}\n");
            LambdaLogger.Log($"With input: {input}\n");
            return AWSLambdaWrapper.TraceAsync(tracerProvider, FunctionHandlerInternalAsync, input, context, propagationContext.ActivityContext);
        }
        private Task FunctionHandlerInternalAsync(NewUser input, ILambdaContext context)
        {
            // This is just an example of function handler and should be replaced by actual code.
            //return $"Welcome: {input.firstName} {input.surname}";
            return Task.CompletedTask;
        }
    }
}