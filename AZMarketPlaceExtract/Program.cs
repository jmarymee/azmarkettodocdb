using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace AZMarketPlaceExtract
{
    class Program
    {
        static void Main(string[] args)
        {
            AZExtract az = new AZExtract();

            //Sets up DocDB. We need the DocDB URI and a RW Key in order for this to work. You can get those from the Azure Portal
            //NOTE: If you haven't created a database and a collection by hand (I recommend you don't - use the string settings in this code) then the code
            // will create them for you. If they already exist, then it will just get a reference to them
            az.InitDocDB().Wait();
            if (!az.IsInit) { return; }

            //This will use a non-authenticated call to get all the entries in the marketplace - as an object list converted from the JSON returned string
            List<object> oList = az.GetAZMInfo();
            foreach(object o in oList)
            {
                //for each object we insert it into DocDB as a separate entity
                //NOTE: When you look at the insert code, there is a BASIC attempt to eliminate duplicates
                // The nature of synch means that we may want to run this again and if there are new entities we don't want to create double entries
                // Thus this code uses the built-in GETHASHCODE function on strings. It is most likely just fine, but there are warnings about using this method
                // to guarantee uniqueness. Therefore if you use this code for production work, you MAY want to replace the GETHASHCODE method with something more robust
                az.insertDocument(o).Wait();
                dynamic oinfo = o;
                Console.Out.WriteLine(String.Format("Inserted identity {0} to DocDB", oinfo.itemDisplayName));
            }
        }
    }

    public class AZExtract
    {
        private DocumentCollection _marketCollection;
        private Database _docDBDataBase;
        private string _docDBName = "AzureMPDB";
        private string _docDBCollectionName = "azureMPCollection";
        private DocumentClient _docDBClient;

        //We use this to ensure that the DocDB init went well
        private bool _isInit = false;
        public bool IsInit
        {
            get { return _isInit; }
        }

        //EXAMPLE: "https://myazuredocdbname.documents.azure.com:443/"
        private string _docDBEndpoint = "";
        private string _docDBRWKey = "";

        public List<object> GetAZMInfo()
        {
            WebClient wc = new WebClient();
            Uri u = new Uri("https://gallery.azure.com/Microsoft.Gallery/galleryitems/?api-version=2015-04-01&includePreview=true");
            string jsonInf = wc.DownloadString(u);
            //File.WriteAllText(@"c:\tools\azm.json", jsonInf); //We can optionally write this JSON to a file for debugging or manual review. I recommend VS Code to view :)
            return JsonConvert.DeserializeObject<List<object>>(jsonInf);
        }

        /// <summary>
        /// This method initializes Document DB for use. We must have an endpoint and key to start
        /// However this code WILL create a new database and/or collection if one or the other does not exist
        /// </summary>
        /// <returns></returns>
        public async Task InitDocDB()
        {
            try
            {
                _docDBClient = new DocumentClient(new Uri(_docDBEndpoint), _docDBRWKey); //Get shared instance var for DocDB Client

                //Retrieves or creates the DocDB database using the passed-in name. The method stores the initialized DB var in a private var of this class/object
                // Try to retrieve the database (Microsoft.Azure.Documents.Database) whose Id is equal to databaseId            
                _docDBDataBase = await RetrieveOrCreateDatabaseAsync();

                //Same for collections. One API call here but it initializes (4) total collections; two for companies and 2 for people
                //await RetrieveOrCreateCollectionsAsync();
                _marketCollection = await RetrieveOrCreateCollectionAsync();

                _isInit = true;
            }
            catch
            {
                _isInit = false;
            }
        }


        /// <summary>
        /// This method attempts to connect to the DocDB database name passed in. If it doesn't exist, it creates it
        /// </summary>
        /// <returns>A DocDB Database reference</returns>
        private async Task<Database> RetrieveOrCreateDatabaseAsync()
        {
            // Try to retrieve the database (Microsoft.Azure.Documents.Database) whose Id is equal to databaseId            
            _docDBDataBase = _docDBClient.CreateDatabaseQuery().Where(db => db.Id == _docDBName).AsEnumerable().FirstOrDefault();

            // If the previous call didn't return a Database, it is necessary to create it
            if (_docDBDataBase == null)
            {
                _docDBDataBase = await _docDBClient.CreateDatabaseAsync(new Database { Id = _docDBName });
            }
            return _docDBDataBase;
        }

        /// <summary>
        /// This method creates a new collection or a reference and then returns it
        /// </summary>
        /// <returns>A DocDB Document Collection Reference</returns>
        private async Task<DocumentCollection> RetrieveOrCreateCollectionAsync()
        {
            _marketCollection = _docDBClient.CreateDocumentCollectionQuery(_docDBDataBase.SelfLink).Where(c => c.Id == _docDBCollectionName).ToArray().FirstOrDefault();

            if(_marketCollection == null)
            {
                _marketCollection = await _docDBClient.CreateDocumentCollectionAsync(_docDBDataBase.SelfLink, new DocumentCollection { Id = _docDBCollectionName });
            }

            return _marketCollection;
        }

        public async Task insertDocument(object entity)
        {
            dynamic obj = entity; //convert to a dynamic object so that we can glomm on the ID that WE want instead of the default. This way we can search for dupes later...
            string docHash = obj.identity; //Identity seems unique enough
            string hashString = docHash.GetHashCode().ToString(); //Convert it to a string for use. NOTE: Here is may you want to replace GETHASHCODE with something more robust
            if (!IsEntryExists(hashString)) //Create if it does not already exist
            {
                obj.id = hashString; //Glomm it on...
                await _docDBClient.CreateDocumentAsync(_marketCollection.SelfLink, obj); //DocDB JUST DEALS with the dynamic object :)
            }
        }

        /// <summary>
        /// This method is used to see if we already have a document in the collection with the same ID. 
        /// </summary>
        /// <param name="hashCode"></param>
        /// <returns></returns>
        public bool IsEntryExists(string hashCode)
        {
            var docs = _docDBClient.CreateDocumentQuery(_marketCollection.SelfLink)
                .Where(r => r.Id == hashCode).ToList();
            if(docs.Any() && docs.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
