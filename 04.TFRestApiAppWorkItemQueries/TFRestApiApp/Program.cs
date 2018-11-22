using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        //static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        static readonly string TFUrl = "https://dev.azure.com/<your_org>/"; // for devops azure 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "your pat";

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            ConnectWithDefaultCreds(TFUrl);

            string teamProject = "";
            string queryPath = "Shared Queries/Product Backlog";
            string queryRootPath = "Shared Queries";
            string sampleQueryName = "My Query";
            //get active user stories
            string queryWiqlList = @"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '"+ teamProject + 
                @"' and [System.WorkItemType] = 'User Story' and [System.State] <> 'Removed' and [System.State] <> 'Closed'";
            //get WBS
            string queryWiqlTree = "SELECT [System.Id] FROM WorkItemLinks WHERE ([Source].[System.TeamProject] = '" + teamProject + 
                "'  AND  [Source].[System.WorkItemType] IN ('Feature', 'User Story')  AND  [Source].[System.State] IN ('New', 'Active', 'Resolved'))" +
                " And ([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward') And ([Target].[System.WorkItemType] IN ('User Story', 'Task')" +
                "  AND  [Target].[System.State] IN ('New', 'Active', 'Resolved')) ORDER BY [Microsoft.VSTS.Common.StackRank], [System.Id] mode(Recursive)";

            Console.WriteLine("List All Queries and Folders");
            GetAllWorkItemQueries(teamProject);

            Console.WriteLine("\nGet Flat List");
            GetQueryResult(queryWiqlList); 

            Console.WriteLine("\nGet Result with Links");
            GetQueryResult(queryWiqlTree); 

            Console.WriteLine("\nRun Query from TFS");
            RunStoredQuery(teamProject, queryPath);  

            Console.WriteLine("\nSample Operations with Query");
            OperateWithQuery(teamProject, queryRootPath, sampleQueryName); 
        }

        /// <summary>
        /// Get Query Struct
        /// </summary>
        /// <param name="project">Team Project Name</param>
        static void GetAllWorkItemQueries(string project)
        {
            List<QueryHierarchyItem> rootQueries = WitClient.GetQueriesAsync(project, QueryExpand.All).Result;

            GetFolderContent(project, rootQueries);
        }

        /// <summary>
        /// Get Content from Query Folders
        /// </summary>
        /// <param name="project">Team Project Name</param>
        /// <param name="queries">Folder List</param>
        static void GetFolderContent(string project, List<QueryHierarchyItem> queries)
        {
            foreach(QueryHierarchyItem query in queries)
            {
                if (query.IsFolder != null && (bool)query.IsFolder)
                {
                    Console.WriteLine("Folder: " + query.Path);
                    if ((bool)query.HasChildren)
                    {
                        QueryHierarchyItem detiledQuery = WitClient.GetQueryAsync(project, query.Path, QueryExpand.All, 1).Result;
                        GetFolderContent(project, detiledQuery.Children.ToList());
                    }
                }
                else
                    Console.WriteLine("Query: " + query.Path);
            }
        }

        /// <summary>
        /// Run query and show result
        /// </summary>
        /// <param name="wiqlStr">Wiql String</param>
        static void GetQueryResult(string wiqlStr)
        {
            WorkItemQueryResult result = RunQueryByWiql(wiqlStr);

            if (result != null)
            {
                if (result.WorkItems != null) // this is Flat List 
                    foreach (var wiRef in result.WorkItems)
                    {
                        var wi = GetWorkItem(wiRef.Id);
                        Console.WriteLine(String.Format("{0} - {1}", wi.Id, wi.Fields["System.Title"].ToString()));
                    }
                else if (result.WorkItemRelations != null) // this is Tree of Work Items or Work Items and Direct Links
                {
                    foreach (var wiRel in result.WorkItemRelations)
                    {
                        if (wiRel.Source == null)
                        {
                            var wi = GetWorkItem(wiRel.Target.Id);
                            Console.WriteLine(String.Format("Top Level: {0} - {1}", wi.Id, wi.Fields["System.Title"].ToString()));
                        }
                        else
                        {
                            var wiParent = GetWorkItem(wiRel.Source.Id);
                            var wiChild = GetWorkItem(wiRel.Target.Id);
                            Console.WriteLine(String.Format("{0} --> {1} - {2}", wiParent.Id, wiChild.Id, wiChild.Fields["System.Title"].ToString()));
                        }
                    }
                }
                else Console.WriteLine("There is no query result");
            }
        }

        /// <summary>
        /// Run Query with Wiql
        /// </summary>
        /// <param name="wiqlStr">Wiql String</param>
        /// <returns></returns>
        static WorkItemQueryResult RunQueryByWiql(string wiqlStr)
        {
            Wiql wiql = new Wiql();
            wiql.Query = wiqlStr;

            return WitClient.QueryByWiqlAsync(wiql).Result;
        }

        /// <summary>
        /// Run stored query on tfs/vsts
        /// </summary>
        /// <param name="project">Team Project Name</param>
        /// <param name="queryPath">Path to Query</param>
        static void RunStoredQuery(string project, string queryPath)
        {
            QueryHierarchyItem query = WitClient.GetQueryAsync(project, queryPath, QueryExpand.Wiql).Result;

            string wiqlStr = query.Wiql;

            if (wiqlStr.Contains("@project")) wiqlStr = wiqlStr.Replace("@project", "'" + project + "'");

            GetQueryResult(wiqlStr);
        }

        /// <summary>
        /// Simple operations woth query
        /// </summary>
        /// <param name="project">Team Project Name</param>
        /// <param name="queryRootPath">Root Folder for Query</param>
        /// <param name="queryName">Query Name</param>
        static void OperateWithQuery(string project, string queryRootPath, string queryName)
        {          
            //Get new and active tasks
            string customWiql = @"SELECT [System.Id], [System.Title], [System.State] FROM WorkItems WHERE [System.TeamProject] = '" + project +
                @"' and [System.WorkItemType] = 'Task' and [System.State] <> 'Closed'";
            //Get new tasks only
            string updatedWiql = @"SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.TeamProject] = '" + project +
                @"' and [System.WorkItemType] = 'Task' and [System.State] == 'New'";

            Console.WriteLine("Create New Query");
            AddQuery(project, queryRootPath, queryName, customWiql);
            RunStoredQuery(project, queryRootPath + "/" + queryName);

            Console.WriteLine("\nUpdate Query");
            EditQuery(project, queryRootPath + "/" + queryName, updatedWiql);
            RunStoredQuery(project, queryRootPath + "/" + queryName);

            Console.WriteLine("\nDelete Query");
            RemoveQuery(project, queryRootPath + "/" + queryName);
        }

        /// <summary>
        /// Create new query
        /// </summary>
        /// <param name="project"></param>
        /// <param name="queryPath"></param>
        /// <param name="QueryName"></param>
        /// <param name="wiqlStr"></param>
        static void AddQuery(string project, string queryPath, string QueryName, string wiqlStr)
        {
            QueryHierarchyItem query = new QueryHierarchyItem();
            query.QueryType = QueryType.Flat;
            query.Name = QueryName;
            query.Wiql = wiqlStr;

            query  = WitClient.CreateQueryAsync(query, project, queryPath).Result;
        }

        /// <summary>
        /// Update Existing Query
        /// </summary>
        /// <param name="project"></param>
        /// <param name="queryPath"></param>
        /// <param name="newWiqlStr"></param>
        static void EditQuery(string project, string queryPath, string newWiqlStr)
        {
            QueryHierarchyItem query = WitClient.GetQueryAsync(project, queryPath).Result;
            query.Wiql = newWiqlStr;

            query = WitClient.UpdateQueryAsync(query, project, queryPath).Result;
        }

        /// <summary>
        /// Remove Existing Query or Folder
        /// </summary>
        /// <param name="project"></param>
        /// <param name="queryPath"></param>
        static void RemoveQuery(string project, string queryPath)
        {
            WitClient.DeleteQueryAsync(project, queryPath);
        }

        /// <summary>
        /// Get one work item
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        static WorkItem GetWorkItem(int Id)
        {
            return WitClient.GetWorkItemAsync(Id).Result;
        }

        #region create new connections
        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            GitClient = Connection.GetClient<GitHttpClient>();
            TfvsClient = Connection.GetClient<TfvcHttpClient>();
            TestManagementClient = Connection.GetClient<TestManagementHttpClient>();
        }

        static void ConnectWithDefaultCreds(string ServiceURL)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssCredentials());
            InitClients(connection);
        }

        static void ConnectWithCustomCreds(string ServiceURL, string User, string Password)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new WindowsCredential(new NetworkCredential(User, Password)));
            InitClients(connection);
        }

        static void ConnectWithPAT(string ServiceURL, string PAT)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssBasicCredential(string.Empty, PAT));
            InitClients(connection);
        }
        #endregion
    }
}
