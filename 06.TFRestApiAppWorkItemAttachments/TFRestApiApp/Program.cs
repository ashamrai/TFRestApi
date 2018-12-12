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
        static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        //static readonly string TFUrl = "https://dev.azure.com/<your_org>/"; // for devops azure 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<your pat>";
        

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            int wiId = -1; // the work item id to upload and download attachmets
            string filePath = @""; //  the file path that will be uploaded
            string destinationFolder = @""; //the existing folder for all attachments from the work item with wiId

            ConnectWithDefaultCreds(TFUrl);

            AddAttachment(wiId, filePath);
            DownloadAttachments(wiId, destinationFolder);
        }

        static void AddAttachment(int WiID, string FilePath)
        {
            AttachmentReference att;
            string[] filePathSplit = FilePath.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            using (FileStream attStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                att = WitClient.CreateAttachmentAsync(attStream, filePathSplit[filePathSplit.Length - 1]).Result; // upload the file

            List<object> references = new List<object>(); //list with references

            references.Add(new
            {
                rel = RelConstants.AttachmentRefStr,
                url = att.Url,
                attributes = new { comment = "Comments for the file " + filePathSplit[filePathSplit.Length - 1] }
            });

            AddWorkItemRelations(WiID, references);
        }

        /// <summary>
        /// Download all atachments from a work item
        /// </summary>
        /// <param name="WIId"></param>
        /// <param name="DestFolder"></param>
        static void DownloadAttachments(int WIId, string DestFolder)
        {
            WorkItem workItem = WitClient.GetWorkItemAsync(WIId, expand: WorkItemExpand.Relations).Result;

            foreach(var rf in workItem.Relations)
            {
                if (rf.Rel == RelConstants.AttachmentRefStr)
                {
                    string[] urlSplit = rf.Url.ToString().Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                    using (Stream attStream = WitClient.GetAttachmentContentAsync(new Guid(urlSplit[urlSplit.Length - 1])).Result) // get an attachment stream
                    using (FileStream destFile = new FileStream(DestFolder + "\\" + rf.Attributes["name"], FileMode.Create, FileAccess.Write)) // create new file
                        attStream.CopyTo(destFile); //copy content to the file
                }
            }
        }

        /// <summary>
        /// Add Relations
        /// </summary>
        /// <param name="WIId"></param>
        /// <param name="References"></param>
        /// <returns></returns>
        static WorkItem AddWorkItemRelations(int WIId, List<object> References)
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();

            foreach (object rf in References)
                patchDocument.Add(new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = rf
                });

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
            public const string AttachmentRefStr = "AttachedFile";
        }
    }
}
