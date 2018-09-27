using System;
using System.Collections.Generic;
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
        private static string _sqsName;
        private const int BATCH_SIZE = 10;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args">
        /// 1. Number of Messages to send
        /// 2. Optional - Confirm or not
        /// </param>
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            _sqsName = config.GetSection("SqsName").Value;
            var options = config.GetAWSOptions();
            

            Console.WriteLine("Profile - {0}", options.Profile);
            Console.WriteLine("Region - {0}", options.Region);
            Console.WriteLine("Queue - {0}", _sqsName);

            Console.Write("Enter How many Messages (Enter to send default messages): ");
            var numberOfMessageAsStr = args.Length == 0 ? Console.ReadLine() : args[0];

            if (!int.TryParse(numberOfMessageAsStr, out var numberOfMessage))
            {
                numberOfMessage = BATCH_SIZE;
            }

            bool.TryParse((args.Length > 1 ? args[1] : "false"), out var proceed);


            SendToQ(options, numberOfMessage, proceed).GetAwaiter().GetResult();
        }


        internal static async Task SendToQ(AWSOptions options, int numberOfMessage, bool confirm)
        {
            var messageCount = 0;
            using (var sqs = options.CreateServiceClient<IAmazonSQS>())
            {

                try
                {
                    var qUrl = await GetQueueUrl(sqs, _sqsName);
                    if (!confirm)
                    {
                        confirm = Confirmation(numberOfMessage, qUrl);
                    }


                    while (confirm && messageCount < numberOfMessage)
                    {
                        var requestMsg = Math.Abs(numberOfMessage - messageCount); 
                        
                        requestMsg = requestMsg > BATCH_SIZE ? BATCH_SIZE : requestMsg; 

                        await SendBatchMessage(sqs, qUrl, requestMsg);

                        messageCount += requestMsg;
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

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\r\nTotal sent {0} messages", messageCount);
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        private static async Task SendBatchMessage(IAmazonSQS amazonSqsClient, string queueUrl, int batchSize)
        {
            var request = new SendMessageBatchRequest
            {
                Entries = new List<SendMessageBatchRequestEntry>() { },
                QueueUrl = queueUrl
            };

            for (var i = 0; i < batchSize; i++)
            {
                request.Entries.Add(CreateNewEntry($"{i}-{batchSize}"));
            }


            var response = await amazonSqsClient.SendMessageBatchAsync(request, CancellationToken.None);

            if (response.Successful.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Successfully sent {0} message", response.Successful.Count);

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                foreach (var success in response.Successful)
                {
                    Console.WriteLine("  For ID: '" + success.Id + "':");
                    Console.WriteLine("    Message ID = " + success.MessageId);
                    //Console.WriteLine("    MD5 of message attributes = " +
                    //                  success.MD5OfMessageAttributes);
                    Console.WriteLine("    MD5 of message body = " +
                                      success.MD5OfMessageBody);
                }
            }

            if (response.Failed.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine("Failed to be sent:");
                
                Console.ForegroundColor = ConsoleColor.Magenta;
                foreach (var fail in response.Failed)
                {
                    Console.WriteLine("  For ID '" + fail.Id + "':");
                    Console.WriteLine("    Code = " + fail.Code);
                    Console.WriteLine("    Message = " + fail.Message);
                    Console.WriteLine("    Sender's fault? = " +
                                      fail.SenderFault);
                }
            }
            
            Console.ForegroundColor = ConsoleColor.White;
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

        private static bool Confirmation(int numberOfMessage, string qUrl)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("Preparing to send {0} messages to {1}", numberOfMessage, qUrl);
            Console.Write("Enter Esc to exit OR Enter any key to proceed! ");
            var key = Console.ReadKey();
            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine("No. User chose to exit process");
                return false;
            }
            Console.WriteLine("Yes");
            Console.WriteLine("===========================================");
            return true;
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

        private static SendMessageBatchRequestEntry CreateNewEntry(string id)
        {
            return new SendMessageBatchRequestEntry
            {
                DelaySeconds = 0,
                Id = id,
                MessageBody = "{\"eventTypeId\":1,\"eventTypeName\":\"Qc\",\"referenceId\":1,\"accounts\":[12],\"body\":{\"fileId\":1,\"versionId\":1,\"statusId\":1,\"statusName\":\"Passed\",\"attributes\":[{\"id\":1,\"name\":\"name\",\"value\":\"a value\"}]}}"
            };
        }

    }
}
