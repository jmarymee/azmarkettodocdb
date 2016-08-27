# azmarkettodocdb
This is example code to pull all Azure Market Place companies from Azure (as JSON) and ingest to a DocDB Collection

It is a small example of how to ingest a lot of JSON (relativley speaking) and place into a schema-less database for query

All you need to do is:
* Clone to your Visual Studio working directory
* Package Restore for things like JSON.NET (NewtonSoft) and Azure DocDB .NET libs
* Add an endpoint URL to the code (it's all in one C# file)
* Add a RW key for your DOC DB Instance
* Edit/Modify the DocDB Database Name and Collection. If you don't then it will use the ones I put in already
* Run the code to completion. There are output strings to show you progress
* Run at intervals if you want to update the DocDB colection with new entries in the AZ Marketplace
* You can also choose to run this as an Azure Web Job at regular intervals if you want to stay in synch

##NOTE: If you use this in production you *may* want to change how the unique IDs are chosen. For simplicity, I chose the C# String function GETHASHCODE which may not guarantee uniqueness in the long run
