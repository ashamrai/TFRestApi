using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
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
        static readonly string AzDOUrl = "https://dev.azure.com/<org>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static WorkItemTrackingProcessHttpClient ProcessHttpClient;
        static TaggingHttpClient TaggingClient;

        static void Main(string[] args)
        {
            string teamProject = "<TeamProjectName>";
            string repoName = "<RepoName>";
            int existingPrId = 0;

            ConnectWithPAT(AzDOUrl, UserPAT);

            AddPRAttachment(teamProject, repoName, existingPrId);

            Console.ReadKey();

            GetPRAttachments(teamProject, repoName, existingPrId);

            Console.ReadKey();

            RemovePRAttachment(teamProject, repoName, existingPrId);

            Console.ReadKey();
        
        }

        /// <summary>
        /// Add attachment to PR
        /// </summary>
        /// <param name="teamProject"></param>
        /// <param name="repoName"></param>
        /// <param name="prId"></param>
        private static void AddPRAttachment(string teamProject, string repoName, int prId)
        {
            string filename = "icon.png";
            var prAttachment = GitClient.CreateAttachmentAsync(new FileStream(filename, FileMode.Open), teamProject, filename,  repoName, prId).Result;

            string commentContent = $@"[{filename}]({prAttachment.Url})";

            CreateNewCommentThread(teamProject, repoName, prId, commentContent);       
        }

        /// <summary>
        /// Download all attachments
        /// </summary>
        /// <param name="teamProject"></param>
        /// <param name="repoName"></param>
        /// <param name="prId"></param>
        private static void GetPRAttachments(string teamProject, string repoName, int prId)
        {
            var attachmets = GitClient.GetAttachmentsAsync(teamProject, repoName, prId).Result;

            foreach (var attachment in attachmets)
            {
                Console.WriteLine($@"{attachment.Id} - {attachment.DisplayName} - {attachment.Url}");

                var fileStream = GitClient.GetAttachmentContentAsync(teamProject, attachment.DisplayName, repoName, prId).Result;

                fileStream.CopyToAsync(new FileStream(attachment.DisplayName, FileMode.OpenOrCreate));
            }
        }

        /// <summary>
        /// Remove attachment from PR
        /// </summary>
        /// <param name="teamProject"></param>
        /// <param name="repoName"></param>
        /// <param name="prId"></param>
        private static void RemovePRAttachment(string teamProject, string repoName, int prId)
        {
            string filename = "icon.png";

            var attachmets = GitClient.GetAttachmentsAsync(teamProject, repoName, prId).Result;

            if ((from a in attachmets where a.DisplayName == filename select a).FirstOrDefault() == null)
            {
                Console.WriteLine("The attachment does not exist");
                return;
            }

            GitClient.DeleteAttachmentAsync(teamProject, filename, repoName, prId).Wait();
        }

        /// <summary>
        /// Start new comment thread
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        /// <param name="PrId"></param>
        /// <param name="Title"></param>
        /// <param name="Status"></param>
        static void CreateNewCommentThread(string TeamProjectName, string RepoName, int PrId, string Title, CommentThreadStatus Status = CommentThreadStatus.Active)
        {
            // from: https://github.com/ashamrai/TFRestApi/tree/master/22.TFRestApiAppCompletePullRequests
            GitPullRequest pr = GitClient.GetPullRequestAsync(TeamProjectName, RepoName, PrId).Result;

            GitPullRequestCommentThread gitThread = new GitPullRequestCommentThread();
            gitThread.Status = Status;
            List<Microsoft.TeamFoundation.SourceControl.WebApi.Comment> comments = new List<Microsoft.TeamFoundation.SourceControl.WebApi.Comment>();
            comments.Add(new Microsoft.TeamFoundation.SourceControl.WebApi.Comment
            { Content = Title });
            gitThread.Comments = comments;

            var thread = GitClient.CreateThreadAsync(gitThread, TeamProjectName, RepoName, PrId).Result;
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
            TaggingClient = Connection.GetClient<TaggingHttpClient>();
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
