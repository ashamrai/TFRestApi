using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;
using Microsoft.VisualStudio.Services.Identity.Client;

namespace TFRestApiApp
{
    class Program
    {
        static class emoji
        {
            public const string smiling_face_with_smiling_eyes = "<span>&#128522;</span>";
            public const string winking_face = "<span>&#128521</span>";
            public const string red_heart = "<span>❤</span>";
            public const string disappointed_face = "<span>&#128542;</span>";
            public const string miling_face_with_open_mouth = "<span>&#128515;</span>";
        }

        static readonly string TFUrl = "https://dev.azure.com/<org>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        #region Azure DevOps clients
        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static TeamHttpClient TeamClient;
        static ReleaseHttpClient ReleaseClient;
        static IdentityHttpClient IdentityClient;
        #endregion

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<team_project_name>";
                int workItemID = 0; //update the id
                string new_comment = string.Format("<div>That`s a cool approach {0} but too complex {1}</div>", emoji.miling_face_with_open_mouth, emoji.disappointed_face);
                string updated_comment = string.Format("<div>That`s a cool approach {0} just do it {1}</div>", emoji.miling_face_with_open_mouth, emoji.red_heart);

                ConnectWithPAT(TFUrl, UserPAT);

                AddWorkitemComment(TeamProjectName, workItemID, new_comment);
                UpdateLastComment(TeamProjectName, workItemID, updated_comment);
                AddReactionToLastComment(TeamProjectName, workItemID, CommentReactionType.Like);
                AddReactionToLastComment(TeamProjectName, workItemID, CommentReactionType.Confused);
                AddReactionToLastComment(TeamProjectName, workItemID, CommentReactionType.Like);
                GetWorkitemComments(TeamProjectName, workItemID);
                RemoveFirstComment(TeamProjectName, workItemID);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Add reaction to the last comment
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="workItemID"></param>
        /// <param name="reactionType"></param>
        private static void AddReactionToLastComment(string teamProjectName, int workItemID, CommentReactionType reactionType)
        {
            CommentList comments = WitClient.GetCommentsAsync(teamProjectName, workItemID).Result;

            var reaction = WitClient.CreateCommentReactionAsync(teamProjectName, workItemID, comments.Comments.ElementAt(0).Id, reactionType).Result;

            Console.WriteLine("{0} - {1}\n", reaction.Type, reaction.Count);
        }

        /// <summary>
        /// Remove reaction from the last comment
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="workItemID"></param>
        /// <param name="reactionType"></param>
        private static void RemoveReactionFromLastComment(string teamProjectName, int workItemID, CommentReactionType reactionType)
        {
            CommentList comments = WitClient.GetCommentsAsync(teamProjectName, workItemID).Result;

            var reaction = WitClient.DeleteCommentReactionAsync(teamProjectName, workItemID, comments.Comments.ElementAt(0).Id, reactionType).Result;

            Console.WriteLine("{0} - {1}\n", reaction.Type, reaction.Count);
        }

        /// <summary>
        /// Get all commeent in work item
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="workItemID"></param>
        private static void GetWorkitemComments(string teamProjectName, int workItemID)
        {
            CommentList comments = WitClient.GetCommentsAsync(teamProjectName, workItemID).Result;

            foreach(var comment in comments.Comments)
            {
                Console.WriteLine("{0} - {1}\n{2}", comment.CreatedDate, comment.CreatedBy.DisplayName, comment.Text);
            }
        }

        /// <summary>
        /// Update the last comment
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="workItemID"></param>
        /// <param name="message"></param>
        private static void UpdateLastComment(string teamProjectName, int workItemID, string message)
        {
            CommentList comments = WitClient.GetCommentsAsync(teamProjectName, workItemID).Result;

            var comment = WitClient.UpdateCommentAsync(new CommentUpdate() { Text = message }, teamProjectName, workItemID, comments.Comments.ElementAt(0).Id).Result;

            Console.WriteLine("{0} - {1}\n{2}", comment.CreatedDate, comment.CreatedBy.DisplayName, comment.Text);
        }

        /// <summary>
        /// Remove the first comment
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="workItemID"></param>
        private static void RemoveFirstComment(string teamProjectName, int workItemID)
        {
            CommentList comments = WitClient.GetCommentsAsync(teamProjectName, workItemID).Result;

            WitClient.DeleteCommentAsync(teamProjectName, workItemID, comments.Comments.ElementAt(comments.Count - 1).Id).Wait();
        }

        /// <summary>
        /// Add new comment to work item
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="workItemID"></param>
        /// <param name="message"></param>
        private static void AddWorkitemComment(string teamProjectName, int workItemID, string message)
        {

            var comment =  WitClient.AddCommentAsync(new CommentCreate() { Text = message }, teamProjectName, workItemID).Result;

            Console.WriteLine("{0} - {1}\n{2}", comment.CreatedDate, comment.CreatedBy.DisplayName, comment.Text);
        }

        #region create new connections

        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            TeamClient = Connection.GetClient<TeamHttpClient>();
            GitClient = Connection.GetClient<GitHttpClient>();
            TfvsClient = Connection.GetClient<TfvcHttpClient>();
            TestManagementClient = Connection.GetClient<TestManagementHttpClient>();
            ReleaseClient = Connection.GetClient<ReleaseHttpClient>();
            IdentityClient = Connection.GetClient<IdentityHttpClient>();
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
