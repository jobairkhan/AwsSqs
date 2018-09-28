using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;

namespace SendAnyMessageToAnySQS
{
    public class Program
    {
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

            var sqsName = config.GetSection("SqsName").Value;
            var options = config.GetAWSOptions();


            Console.WriteLine("Profile - {0}", options.Profile);
            Console.WriteLine("Region - {0}", options.Region);
            Console.WriteLine("Queue - {0}", sqsName);

            
            if (!int.TryParse(
                GetNumberOfMessagesToSend(args, config), 
                out var numberOfMessage))
            {
                numberOfMessage = BATCH_SIZE;
            }


            var messageCount =
                SendToQ(
                    options,
                    sqsName,
                    numberOfMessage,
                    GetValue(args, config)
                    ).GetAwaiter()
                     .GetResult();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\r\nTotal sent {0} messages", messageCount);
            Console.WriteLine("Press Enter to exit...");
            if (args.Length == 0) Console.ReadLine();
        }

        private static string GetNumberOfMessagesToSend(string[] args, IConfiguration config)
        {
            var numberOfMessageAsStr = config.GetSection("NumberOfMessages")?.Value ?? string.Empty;

            if (args.Length > 0)
            {
                numberOfMessageAsStr = args[0];
            }
            else if(string.IsNullOrWhiteSpace(numberOfMessageAsStr))
            {
                Console.Write("Enter How many Messages (Enter to send default messages): ");
                numberOfMessageAsStr = Console.ReadLine();
            }
            
            return numberOfMessageAsStr;
        }

        private static bool GetValue(string[] args, IConfiguration config)
        {
            var acceptedAnswers = new[] { "yes", "1", "true", "y" };
            string val = null;
            if ((args?.Length ?? 0) > 1)
            {
                Console.WriteLine("args {0} {1}", args[0], args[1]);

                val = args[1].ToLowerInvariant();

            }
            else
            {
                val = config.GetSection("DoNotPromptConfirmation")?.Value.ToLowerInvariant() ?? "NO";
            }
            
            return acceptedAnswers.Any(x => x == val);
        }

        static int _messageCount = 0;
        internal static async Task<int> SendToQ(AWSOptions options, string sqsQName, int numberOfMessage, bool confirm)
        {
            try
            {
                using (var sqs = options.CreateServiceClient<IAmazonSQS>())
                {
                    var qUrl = await GetQueueUrl(sqs, sqsQName);
                    if (!confirm)
                    {
                        confirm = Confirmation(numberOfMessage, qUrl);
                    }

                    while (confirm && _messageCount < numberOfMessage)
                    {
                        var requestMsg = Math.Abs(numberOfMessage - _messageCount);

                        requestMsg = requestMsg > BATCH_SIZE ? BATCH_SIZE : requestMsg;

                        await SendBatchMessage(sqs, qUrl, requestMsg);

                        _messageCount += requestMsg;
                    }

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
            return _messageCount;
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
                request.Entries.Add(CreateNewEntry($"{i + _messageCount}"));
            }


            var response = await amazonSqsClient.SendMessageBatchAsync(request, CancellationToken.None);
            WriteToConsole(response.Successful);
            WriteToConsole(response.Failed);

            Console.ForegroundColor = ConsoleColor.White;
        }

        private static SendMessageBatchRequestEntry CreateNewEntry(string id)
        {
            return new SendMessageBatchRequestEntry
            {
                DelaySeconds = 0,
                Id = id,
                MessageBody = "{\"eventTypeId\":1,\"eventTypeName\":\"Qc\",\"referenceId\":1,\"accounts\":[12],\"body\":{\"fileId\":1,\"versionId\":1,\"statusId\":1,\"statusName\":\"Passed\",\"attributes\":[{\"id\":1,\"name\":\"" + Guid.NewGuid() + "\",\"value\":\"" + id + "\"}]}}"
            };
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

        private static void ConsoleError(AmazonServiceException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Caught AWS Exception: " + ex.Message);
            Console.WriteLine("Response Status Code: " + ex.StatusCode);
            Console.WriteLine("Error Code: " + ex.ErrorCode);
            Console.WriteLine("Error Type: " + ex.ErrorType);
            Console.WriteLine("Request ID: " + ex.RequestId);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void WriteToConsole(List<BatchResultErrorEntry> responses)
        {
            if (responses.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine("Failed to be sent:");

                Console.ForegroundColor = ConsoleColor.Magenta;
                foreach (var fail in responses)
                {
                    Console.WriteLine("  For ID '" + fail.Id + "':");
                    Console.WriteLine("    Code = " + fail.Code);
                    Console.WriteLine("    Message = " + fail.Message);
                    Console.WriteLine("    Sender's fault? = " +
                                      fail.SenderFault);
                }
            }
        }

        private static void WriteToConsole(List<SendMessageBatchResultEntry> responses)
        {
            if (responses.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Successfully sent {0} message", responses.Count);

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                foreach (var success in responses)
                {
                    Console.WriteLine("  For ID: '" + success.Id + "':");
                    Console.WriteLine("    Message ID = " + success.MessageId);
                    //Console.WriteLine("    MD5 of message attributes = " +
                    //                  success.MD5OfMessageAttributes);
                    Console.WriteLine("    MD5 of message body = " +
                                      success.MD5OfMessageBody);
                }
            }
        }
    }
}
