using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;

namespace SendAnyMessageToAnySQS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            _config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            var options = _config.GetAWSOptions();

            Console.WriteLine("Profile - {0}", options.Profile);
            Console.WriteLine("Region - {0}", options.Region);

            Console.Write("Enter How many Messages (Enter to send default messages): ");
            var numberOfMessageAsStr = args.Length == 0 ? Console.ReadLine() : args[0];

            if (!int.TryParse(numberOfMessageAsStr, out var numberOfMessage))
            {
                numberOfMessage = BATCH_SIZE;
            }

            SendToQ(options, numberOfMessage).GetAwaiter().GetResult();
        }

        private static IConfiguration _config;
        private const string QUEUE_NAME = "webhooks_events";
        private const int BATCH_SIZE = 1;
        private const string AWS_ACCOUNT_ID = "130075576898";

        internal static async Task SendToQ(AWSOptions options, int numberOfMessage)
        {
            var messageCount = 0;
            var config = new AmazonSQSConfig { RegionEndpoint = options.Region };

            //var client = new AmazonSQSClient("AKIAJZI7XEZ5ALU5V7IQ", "Q4aBb8RcnJ7vslgmx1bN4tm1bJBfmsMZGf+XhdOO", config);
            //var queueUrl = await GetQueueUrl(client, QUEUE_NAME);
            using (var sqs = options.CreateServiceClient<IAmazonSQS>())
            {

                try
                {
                    var qUrl = await GetQueueUrl(sqs, QUEUE_NAME);

                    Console.WriteLine("===========================================");
                    Console.WriteLine("Preparing to send {0} messages to {1}", numberOfMessage, qUrl);
                    Console.WriteLine("Enter to proceed");
                    Console.WriteLine("===========================================\n");
                    Console.ReadLine();


                    while (messageCount < numberOfMessage)
                    {
                        await SendBatchMessage(sqs, qUrl, BATCH_SIZE);

                        messageCount += BATCH_SIZE;
                    }
                }
                catch (AmazonSQSException ex)
                {
                    ConsoleError(ex);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Caught Exception: " + ex.Message);
                }
            }

            Console.WriteLine("Total sent {0} messages", messageCount);
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        private static void ConsoleError(AmazonSQSException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Caught AWS Exception: " + ex.Message);
            Console.WriteLine("Response Status Code: " + ex.StatusCode);
            Console.WriteLine("Error Code: " + ex.ErrorCode);
            Console.WriteLine("Error Type: " + ex.ErrorType);
            Console.WriteLine("Request ID: " + ex.RequestId);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static async Task SendBatchMessage(IAmazonSQS amazonSqsClient, string queueUrl, int batchSize)
        {
            var request = new SendMessageBatchRequest
            {
                Entries = new List<SendMessageBatchRequestEntry>() {  },
                QueueUrl = queueUrl
            };

            for (int i = 0; i < batchSize; i++)
            {
                request.Entries.Add(new SendMessageBatchRequestEntry());
            }


            var response = await amazonSqsClient.SendMessageBatchAsync(request, CancellationToken.None);

            if (response.Successful.Count > 0)
            {
                Console.WriteLine("Successfully sent:");

                foreach (var success in response.Successful)
                {
                    Console.WriteLine("  For ID: '" + success.Id + "':");
                    Console.WriteLine("    Message ID = " + success.MessageId);
                    Console.WriteLine("    MD5 of message attributes = " +
                                      success.MD5OfMessageAttributes);
                    Console.WriteLine("    MD5 of message body = " +
                                      success.MD5OfMessageBody);
                }

                //Console.WriteLine("Successfully sent {0} messages with batch id ({1})", response.Successful.Count, _batchId);
                //_messageCount += response.Successful.Count;
            }

            if (response.Failed.Count > 0)
            {
                Console.WriteLine("Failed to be sent:");

                foreach (var fail in response.Failed)
                {
                    Console.WriteLine("  For ID '" + fail.Id + "':");
                    Console.WriteLine("    Code = " + fail.Code);
                    Console.WriteLine("    Message = " + fail.Message);
                    Console.WriteLine("    Sender's fault? = " +
                                      fail.SenderFault);
                }
            }
        }


        private static async Task<string> GetQueueUrl(IAmazonSQS sqs, string qName)
        {
            var sqsRequest = new GetQueueUrlRequest
            {
                QueueName = qName,
                //QueueOwnerAWSAccountId = AWS_ACCOUNT_ID
            };

            var createQueueResponse = await sqs.GetQueueUrlAsync(sqsRequest);

            Console.WriteLine(createQueueResponse.QueueUrl);
            return createQueueResponse.QueueUrl;
        }
    }
}
