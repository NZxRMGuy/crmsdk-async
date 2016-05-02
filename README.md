# crmsdk-async
The CRM SDK that ship out of the box doesn't provide async/await support, therefore, building responsive applications are tricky, long winded and slow if you're wanting to perfom huge data operations.

This piece of code aims to:
  1. Add async/await support for each of the sdk methods (execute, retrieve, create, delete, etc...)
  2. Remove internal connection blocking to perfom parallel calls to the web service

EXAMPLE
  Connect to the web service first by using the Organization endpoint; http://server/orgname/XRMServices/2011/Organization.svc
  
    var osm = new OrganizationServiceManager(ConfigurationManager.AppSettings["crm.sdkurl.org"]);

  To remove the internal connection blocking, call the GetProxy method, this method clones the current connection with all required info to bypass the internal thread blocking allowing you to perform parallel calls against the web service
  
    osm.GetProxy()

  Execute a QueryExpression and retrieve results
  
    Task<EntityCollection> retrieveMultipleTask = osm.GetProxy().RetrieveMultipleAsync(query);
    var records = retrieveMultipleTask.Result;

Peek into Program.cs to see more examples

Enjoy!
