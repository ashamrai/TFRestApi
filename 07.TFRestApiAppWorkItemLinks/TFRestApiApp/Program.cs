using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        //static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        static readonly string TFUrl = "https://dev.azure.com/<org>/"; // for devops azure 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<your_pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate


        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            string teamProject = ""; // team project for new work item
            int sourceId = -1; // existing work item to duplicate
            int parentId = -1; // existing work item to add as parent
            
            ConnectWithPAT(TFUrl, UserPAT);

            var newWi = DuplicateWorkItem(teamProject, sourceId); // create a duplicate and link it
            RemoveWorkItemLink(sourceId, newWi.Id.Value, RelConstants.DuplicateRefStr); // remove the duplicate
            UpdateWorkItemLink(sourceId, newWi.Id.Value, RelConstants.RelatedRefStr); // update comments and unlock links

            if (parentId > 0) AddParentLink(newWi.Id.Value, parentId);
        }   
        

        /// <summary>
        /// Create a duplicate
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="DuplicateId"></param>
        /// <returns></returns>
        static WorkItem DuplicateWorkItem(string TeamProjectName, int DuplicateId)
        {
            WorkItem wi = WitClient.GetWorkItemAsync(DuplicateId, expand: WorkItemExpand.Relations).Result;

            Console.WriteLine("Duplicate work item {0}: {1}", wi.Id.Value, wi.Fields["System.Title"].ToString());

            Dictionary<string, object> fields = new Dictionary<string, object>();

            List<string> fieldsToCopy = new List<string>();
            fieldsToCopy.Add("System.Title");
            fieldsToCopy.Add("System.AssignedTo");
            fieldsToCopy.Add("System.Description");

            //check and copy the fileds
            foreach(string fieldName in fieldsToCopy) if (wi.Fields.ContainsKey(fieldName)) fields.Add(fieldName, wi.Fields[fieldName]);

            //copy links, skip childs and duplicates
            if (wi.Relations != null)
                foreach (var lnk in wi.Relations)
                    if (lnk.Rel != RelConstants.ChildRefStr && lnk.Rel != RelConstants.DuplicateRefStr)
                        fields.Add(RelConstants.LinkKeyForDict + lnk.Rel + lnk.Url, lnk);

            //add duplicate link to source work item
            fields.Add(RelConstants.LinkKeyForDict + RelConstants.DuplicateOfRefStr + DuplicateId, // to use as unique key
                CreateNewLinkObject(RelConstants.DuplicateOfRefStr, wi.Url, "Duplicate " + wi.Id));

            //add related link and lock it
            fields.Add(RelConstants.LinkKeyForDict + RelConstants.RelatedRefStr + DuplicateId, // to use as unique key
                CreateNewLinkObject(RelConstants.RelatedRefStr, wi.Url, IsLocked: true));

            var dupWi = SubmitWorkItem(fields, 0, TeamProjectName, wi.Fields["System.WorkItemType"].ToString());

            Console.WriteLine("New work item: " + dupWi.Id.Value);

            return dupWi;
        }

        /// <summary>
        /// Create a link object
        /// </summary>
        /// <param name="RelName"></param>
        /// <param name="RelUrl"></param>
        /// <param name="Comment"></param>
        /// <param name="IsLocked"></param>
        /// <returns></returns>
        static object CreateNewLinkObject(string RelName, string RelUrl, string Comment = null, bool IsLocked = false)
        {
            return new
            {
                rel = RelName,
                url = RelUrl,
                attributes = new
                {
                    comment = Comment,
                    isLocked = IsLocked // you must be an administrator to lock a link
                }
            };
        }

        /// <summary>
        /// Remove links
        /// </summary>
        /// <param name="SourceId"></param>
        /// <param name="TargetId"></param>
        /// <param name="LinkName"></param>
        /// <returns></returns>
        static WorkItem RemoveWorkItemLink(int SourceId, int TargetId, string LinkName = "")
        {
            WorkItem wi = WitClient.GetWorkItemAsync(SourceId, expand: WorkItemExpand.Relations).Result;

            if (wi.Relations == null || wi.Relations.Count == 0) return null;

            JsonPatchDocument patchDocument = new JsonPatchDocument();

            Console.WriteLine("Remove links from work item {0}:{1}", wi.Id.Value, wi.Fields["System.Title"].ToString());

            for (int i = wi.Relations.Count - 1; i > 0; i--)
            {
                bool toremove = false;
                if (wi.Relations[i].Url.EndsWith("_apis/wit/workItems/" + TargetId))
                    if (LinkName != "")
                    {
                        if (wi.Relations[i].Rel == LinkName) toremove = true;
                    }
                    else toremove = true;

                if (toremove)
                {
                    patchDocument.Add(new JsonPatchOperation()
                    {
                        Operation = Operation.Remove,
                        Path = "/relations/" + i
                    });

                    Console.WriteLine("Link to remove:\n\tRel - {0}\n\tUrl - {1}", wi.Relations[i].Rel, wi.Relations[i].Url);
                }
            }

            return WitClient.UpdateWorkItemAsync(patchDocument, SourceId).Result;
        }

        /// <summary>
        /// Update links
        /// </summary>
        /// <param name="SourceId"></param>
        /// <param name="TargetId"></param>
        /// <param name="LinkName"></param>
        /// <returns></returns>
        static WorkItem UpdateWorkItemLink(int SourceId, int TargetId, string LinkName = "")
        {
            WorkItem wi = WitClient.GetWorkItemAsync(SourceId, expand: WorkItemExpand.Relations).Result;

            if (wi.Relations == null || wi.Relations.Count == 0) return null;

            JsonPatchDocument patchDocument = new JsonPatchDocument();

            Console.WriteLine("Update links in work item {0}:{1}", wi.Id.Value, wi.Fields["System.Title"].ToString());

            for (int i = wi.Relations.Count - 1; i > 0; i--)
            {
                bool toupdate = false;
                if (wi.Relations[i].Url.EndsWith("_apis/wit/workItems/" + TargetId))
                    if (LinkName != "")
                    {
                        if (wi.Relations[i].Rel == LinkName) toupdate = true;
                    }
                    else toupdate = true;

                if (toupdate)
                {
                    patchDocument.Add(new JsonPatchOperation()
                    {
                        Operation = Operation.Add,
                        Path = "/relations/" + i,
                        Value = CreateNewLinkObject(wi.Relations[i].Rel, wi.Relations[i].Url, "Updated comment")
                    });

                    Console.WriteLine("Link to update:\n\tRel - {0}\n\tUrl - {1}", wi.Relations[i].Rel, wi.Relations[i].Url);
                }
            }

            return WitClient.UpdateWorkItemAsync(patchDocument, SourceId).Result;
        }

        /// <summary>
        /// Add new parent link to existing work item
        /// </summary>
        /// <param name="WiId"></param>
        /// <param name="ParentWiId"></param>
        /// <returns></returns>
        static WorkItem AddParentLink(int WiId, int ParentWiId)
        {
            WorkItem wi = WitClient.GetWorkItemAsync(WiId, expand: WorkItemExpand.Relations).Result;
            bool parentExists = false;

            // check existing parent link
            if (wi.Relations != null)
                if (wi.Relations.Where(x => x.Rel == RelConstants.ParentRefStr).FirstOrDefault() != null) 
                    parentExists = true;

            if (!parentExists)
            {
                WorkItem parentWi = WitClient.GetWorkItemAsync(ParentWiId).Result; // get parent to retrieve its url

                Dictionary<string, object> fields = new Dictionary<string, object>();

                fields.Add(RelConstants.LinkKeyForDict + RelConstants.ParentRefStr + parentWi.Id, // to use as unique key
                CreateNewLinkObject(RelConstants.ParentRefStr, parentWi.Url, "Parent " + parentWi.Id));

                return SubmitWorkItem(fields, WiId);
            }

            Console.WriteLine("Work Item " + WiId + " contains a parent link");

            return null;
        }

        /// <summary>
        /// Create or update a work item
        /// </summary>
        /// <param name="WIId"></param>
        /// <param name="References"></param>
        /// <returns></returns>
        static WorkItem SubmitWorkItem(Dictionary<string, object> Fields, int WIId = 0, string TeamProjectName = "", string WorkItemTypeName = "")
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();

            foreach (var key in Fields.Keys)
                patchDocument.Add(new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = (key.StartsWith(RelConstants.LinkKeyForDict)) ? "/relations/-" : "/fields/" + key,
                    Value = Fields[key]
                });

            if (WIId == 0) return WitClient.CreateWorkItemAsync(patchDocument, TeamProjectName, WorkItemTypeName).Result; // create new work item

            return WitClient.UpdateWorkItemAsync(patchDocument, WIId).Result; // return updated work item
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

        class RelConstants
        {
            //https://docs.microsoft.com/en-us/azure/devops/boards/queries/link-type-reference?view=vsts

            public const string RelatedRefStr = "System.LinkTypes.Related";
            public const string ChildRefStr = "System.LinkTypes.Hierarchy-Forward";
            public const string ParentRefStr = "System.LinkTypes.Hierarchy-Reverse";            
            public const string DuplicateRefStr = "System.LinkTypes.Duplicate-Forward";
            public const string DuplicateOfRefStr = "System.LinkTypes.Duplicate-Reverse";
            public const string SuccessorRefStr = "System.LinkTypes.Dependency-Forward";
            public const string PredecessorRefStr = "System.LinkTypes.Dependency-Reverse";
            public const string TestedByRefStr = "Microsoft.VSTS.Common.TestedBy-Forward";
            public const string TestsRefStr = "Microsoft.VSTS.Common.TestedBy-Reverse";
            public const string TestCaseRefStr = "Microsoft.VSTS.TestCase.SharedStepReferencedBy-Forward";
            public const string SharedStepsRefStr = "Microsoft.VSTS.TestCase.SharedStepReferencedBy-Reverse";
            public const string AffectsRefStr = "Microsoft.VSTS.Common.Affects-Forward";
            public const string AffectedByRefStr = "Microsoft.VSTS.Common.Affects-Reverse";            
            public const string AttachmentRefStr = "AttachedFile";
            public const string HyperLinkRefStr = "Hyperlink";
            public const string ArtifactLinkRefStr = "ArtifactLink";

            public const string LinkKeyForDict = "<NewLink>"; // key for dictionary to separate a link from fields            
        }
    }
}
