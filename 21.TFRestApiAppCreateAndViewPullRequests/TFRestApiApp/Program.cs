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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<org>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        const string ArtigactLinkName = "ArtifactLink";
        const string PRUrlTemplate = "vstfs:///Git/PullRequestId/{0}%2F{1}%2F{2}";
        const string PRLinkName = "Pull Request";

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<Team Project Name>";
                string GitRepo = "<GIT Repo Name>";
                string SourceRef = "refs/heads/<source branch>", TargetRef = "refs/heads/<target branch>";
                int[] wiIds = { };

                ConnectWithPAT(TFUrl, UserPAT);

                Console.WriteLine("\n\nACTIVE PRs\n\n");
                ViewPullRequests(TeamProjectName, GitRepo);
                Console.WriteLine("\n\nCOMPLETED PRs\n\n");
                ViewPullRequests(TeamProjectName, GitRepo, true, TargetRef);
                int prId = CreatePullRequest(TeamProjectName, GitRepo, SourceRef, TargetRef, wiIds);
                CreateReviewTask(TeamProjectName, GitRepo, prId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// View detiled information of active pull requests
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="GitRepo"></param>
        private static void ViewPullRequests(string TeamProjectName, string GitRepo, bool CompletedPRs = false, string TargetRef = "")
        {
            if (CompletedPRs && TargetRef == "")
            {
                Console.WriteLine("Define a target branch reference");
                return;
            }

            var pullRequests = (CompletedPRs)? 
                GitClient.GetPullRequestsAsync(TeamProjectName, GitRepo, new GitPullRequestSearchCriteria { Status = PullRequestStatus.Completed, TargetRefName = TargetRef }, top: 10).Result : 
                GitClient.GetPullRequestsAsync(TeamProjectName, GitRepo, null ).Result;

            foreach (var pullRequest in pullRequests)
            {
                Console.WriteLine("+================PULL REQUEST=======================================================");
                Console.WriteLine("ID: {0} | TITLE: {1}", pullRequest.PullRequestId, pullRequest.Title);
                Console.WriteLine("AUTHOR: {0} | STATUS: {1}", pullRequest.CreatedBy.DisplayName, pullRequest.Status.ToString());
                Console.WriteLine("SOURCEREF: {0} | TARGETREF: {1}", pullRequest.SourceRefName, pullRequest.TargetRefName);
                Console.WriteLine("Description:\n{0}", pullRequest.Description);
                
                var pullTheads = GitClient.GetThreadsAsync(TeamProjectName, GitRepo, pullRequest.PullRequestId).Result;

                if (pullTheads.Count > 0)
                    Console.WriteLine("+------------------COMMENTS---------------------------------------------------------");

                for (int i = 0; i < pullTheads.Count; i++)
                {
                    if (i == 0)
                    {
                        Console.WriteLine("\n{0}", pullTheads[i].Comments[0].Content);
                        Console.WriteLine("STATUS: {0} | AUTHOR: {1}", pullTheads[i].Status.ToString(), pullTheads[i].Comments[0].Author.DisplayName);
                    }
                    for (int c = 1; c < pullTheads[i].Comments.Count; c++)
                    {
                        Console.WriteLine("\t\t{0}", pullTheads[i].Comments[c].Content);
                        Console.WriteLine("\t\tAUTHOR: {0}", pullTheads[i].Comments[c].Author.DisplayName);
                    }
                }

                var workItemRefs = GitClient.GetPullRequestWorkItemRefsAsync(TeamProjectName, GitRepo, pullRequest.PullRequestId).Result;

                if (workItemRefs.Count > 0)
                {
                    Console.WriteLine("+------------------WORK ITEMS-------------------------------------------------------");

                    foreach (var workItemRef in workItemRefs)
                    {
                        int wiId = 0;
                        if (!int.TryParse(workItemRef.Id, out wiId)) continue;

                        var workItem = WitClient.GetWorkItemAsync(wiId).Result;

                        Console.WriteLine("{0,10} {1}", workItem.Id, workItem.Fields["System.Title"]);
                    }
                }

                var commits = GitClient.GetPullRequestCommitsAsync(TeamProjectName, GitRepo, pullRequest.PullRequestId).Result;

                Console.WriteLine("+------------------COMMITS----------------------------------------------------------");

                foreach (var commit in commits)
                {
                    Console.WriteLine("{0} {1}", commit.CommitId.Substring(0, 8), commit.Comment);
                    GitCommitChanges changes = GitClient.GetChangesAsync(TeamProjectName, commit.CommitId, GitRepo).Result;

                    foreach (var change in changes.Changes)
                        Console.WriteLine("{0}: {1}", change.ChangeType, change.Item.Path);
                }
            }
        }


        /// <summary>
        ///  Create new Pull Request
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        /// <param name="SourceRef"></param>
        /// <param name="TargetRef"></param>
        /// <param name="WorkItems"></param>
        /// <returns></returns>
        static int CreatePullRequest(string TeamProjectName, string RepoName, string SourceRef, string TargetRef, int [] WorkItems)
        {
            GitPullRequest pr = new GitPullRequest();
            pr.Title = pr.Description = String.Format("PR from {0} into {1} ", SourceRef, TargetRef);
            pr.SourceRefName = SourceRef;
            pr.TargetRefName = TargetRef;

            if (WorkItems != null && WorkItems.Length > 0)
            {
                List<ResourceRef> wiRefs = new List<ResourceRef>();

                foreach (int wiId in WorkItems)
                {
                    WorkItem workItem = WitClient.GetWorkItemAsync(wiId).Result;

                    wiRefs.Add(new ResourceRef { Id = workItem.Id.ToString(), Url = workItem.Url });
                }

                pr.WorkItemRefs = wiRefs.ToArray();
            }

            var newPr = GitClient.CreatePullRequestAsync(pr, TeamProjectName, RepoName).Result;

            Console.WriteLine("PR was created: " + newPr.PullRequestId);

            return newPr.PullRequestId;
        }

        /// <summary>
        /// Create a new task and link to an existing PR
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        /// <param name="PullRequestId"></param>
        /// <param name="ParentWiId"></param>
        /// <param name="AssignedTo"></param>
        /// <returns></returns>
        static int CreateReviewTask(string TeamProjectName, string RepoName, int PullRequestId, int ParentWiId = 0, string AssignedTo = null)
        {
            GitPullRequest pr = GitClient.GetPullRequestAsync(TeamProjectName, RepoName, PullRequestId).Result;

            List<WiField> fields = new List<WiField>();

            fields.Add(new WiField { FieldName = "System.Title", FiledValue = pr.Title });
            fields.Add(new WiField { FieldName = "System.Description", FiledValue = pr.Description });

            if (AssignedTo != null) fields.Add(new WiField { FieldName = "System.AssignedTo", FiledValue = AssignedTo });

            if (ParentWiId != 0)
            {
                WorkItem  parentWi = WitClient.GetWorkItemAsync(ParentWiId).Result;

                fields.Add(new WiField
                {
                    FieldName = RelConstants.LinkKeyForDict,
                    FiledValue = CreateNewLinkObject(RelConstants.ParrentRefStr, parentWi.Url, null, "Child review task")
                });
            }

            var TeamProject = ProjectClient.GetProject(TeamProjectName).Result;
            var TeamRepo = GitClient.GetRepositoryAsync(TeamProjectName, RepoName).Result;

            fields.Add(new WiField
            {
                FieldName = RelConstants.LinkKeyForDict,
                FiledValue = CreateNewLinkObject(ArtigactLinkName,
                String.Format(PRUrlTemplate, TeamProject.Id, TeamRepo.Id, pr.PullRequestId),
                PRLinkName, "Review this PR")
            });


            return (int)SubmitWorkItem(fields, 0, TeamProjectName, "Task").Id;
        }

        /// <summary>
        /// Create or update a work item
        /// </summary>
        /// <param name="Fields"></param>
        /// <param name="WIId"></param>
        /// <param name="TeamProjectName"></param>
        /// <param name="WorkItemTypeName"></param>
        /// <returns></returns>
        static WorkItem SubmitWorkItem(List<WiField> Fields, int WIId = 0, string TeamProjectName = "", string WorkItemTypeName = "")
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();

            foreach (var field in Fields)
                patchDocument.Add(new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = (field.FieldName.StartsWith(RelConstants.LinkKeyForDict)) ? "/relations/-" : "/fields/" + field.FieldName,
                    Value = field.FiledValue
                });

            if (WIId == 0) return WitClient.CreateWorkItemAsync(patchDocument, TeamProjectName, WorkItemTypeName).Result; // create new work item

            return WitClient.UpdateWorkItemAsync(patchDocument, WIId).Result; // return updated work item
        }

        /// <summary>
        /// Create object to define a link
        /// </summary>
        /// <param name="RelName"></param>
        /// <param name="RelUrl"></param>
        /// <param name="Name"></param>
        /// <param name="Comment"></param>
        /// <returns></returns>
        static object CreateNewLinkObject(string RelName, string RelUrl, string Name, string Comment)
        {
            return new
            {
                rel = RelName,
                url = RelUrl,
                attributes = new
                {
                    name = Name,
                    comment = Comment
                }
            };
        }

        
        public class WiField
        {
            public string FieldName;
            public object FiledValue;
        }

        public class RelConstants
        {
            //https://docs.microsoft.com/en-us/azure/devops/boards/queries/link-type-reference?view=vsts

            public const string RelatedRefStr = "System.LinkTypes.Related";
            public const string ChildRefStr = "System.LinkTypes.Hierarchy-Forward";
            public const string ParrentRefStr = "System.LinkTypes.Hierarchy-Reverse";
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
