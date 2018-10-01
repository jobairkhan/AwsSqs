# AwsSqs
A .net core console app to send messages to AWS SQS. I have created this to measure the performance matrics of my AWS Lambdas


# Send Messages To SQS

## Configuration 
Before using this tool please change the app settings accordingly

- AWS : Aws related settings which is required for AWS .Net SDK to populate `Amazon.Extensions.NETCore.Setup.AWSOption`.
  - Profile : name of the save profile in your machine
  - Region : AWS Region 
- SqsName : Queue name that you want to send messages
- NumberOfMessages : number of meeages you want to send to the queue
- DoNotPromptConfirmation : ["yes", "1", "true", "y"] any of these values will not prompt any confirmation, otherwise system will wait for the user to enter the `Enter` key


## How to use

### EXE

You can run the exe and follow the process. To run the exe open the command prompt and go to the project location any type `dotnet run`

### IDE

Open the app from Visual Studio or VSCode and run the application

### Command Line

You could pass number of meeages and confirmation as arguments. For example

```cmd
dotnet run 10 y
```

 
