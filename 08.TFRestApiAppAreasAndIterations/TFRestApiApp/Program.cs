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
        static readonly string UserPAT = ""; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=vsts


        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            try
            {
                string teamProject = ""; // team project for areas and iterations

                ConnectWithDefaultCreds(TFUrl);            
                //ConnectWithPAT(TFUrl, UserPAT);

                Console.WriteLine("Manage Areas\n");
                ManageAreas(teamProject);
                Console.WriteLine("\n\nManage Iterations\n");
                ManageIterations(teamProject);                
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }                 

        static void ManageIterations(string TeamProjectName)
        {
            WorkItemClassificationNode newNode = CreateIteration(TeamProjectName, @"R2"); //Add iteraion R2
            PrintNodeInfo(newNode);
            newNode = CreateIteration(TeamProjectName, @"R2.1", ParentIterationPath: @"R2"); //Add iteraion R2\R2.1
            PrintNodeInfo(newNode);
            newNode = CreateIteration(TeamProjectName, @"Ver1", new DateTime(2019, 1, 1), new DateTime(2019, 1, 7), @"R2\R2.1"); //Add iteraion R2\R2.1\Ver1
            PrintNodeInfo(newNode);
            newNode = CreateIteration(TeamProjectName, @"Ver2", new DateTime(2019, 1, 7), new DateTime(2019, 1, 14), @"R2\R2.1"); //Add iteraion R2\R2.1\Ver2
            PrintNodeInfo(newNode);
            newNode = CreateIteration(TeamProjectName, @"Ver3", new DateTime(2019, 1, 14), new DateTime(2019, 1, 21), @"R2\R2.1"); //Add iteraion R2\R2.1\Ver3
            PrintNodeInfo(newNode);
            newNode = CreateIteration(TeamProjectName, @"R2.2", ParentIterationPath: @"R2"); //Add iteraion R2\R2.2
            PrintNodeInfo(newNode);
            newNode = CreateIteration(TeamProjectName, @"Ver1", new DateTime(2019, 2, 1), new DateTime(2019, 2, 7), @"R2\R2.2"); //Add iteraion R2\R2.2\Ver1
            PrintNodeInfo(newNode);

            newNode = RenameClassificationNode(TeamProjectName, @"R2\R2.2\Ver1", @"Ver1.1", TreeStructureGroup.Iterations); //Rename iteration R2\R2.2\Ver1 to R2\R2.2\Ver1.1
            PrintNodeInfo(newNode);

            newNode = MoveClassificationNode(TeamProjectName, @"R2\R2.1\Ver3", @"R2\R2.2", TreeStructureGroup.Iterations); //Move iteration R2\R2.1\Ver3 to R2\R2.2\Ver3
            PrintNodeInfo(newNode);

            newNode = ReplanIteration(TeamProjectName, @"R2\R2.2\Ver1.1", new DateTime(2019, 3, 1), new DateTime(2019, 3, 7)); //Update dates for R2\R2.2\Ver1.1
            PrintNodeInfo(newNode);

            DeleteClassificationNode(TeamProjectName, @"R2\R2.2", @"R2", TreeStructureGroup.Iterations); // Delete iteration tree R2\R2.2 and move work items to iteration R2
        }

        static void ManageAreas(string TeamProjectName)
        {
            WorkItemClassificationNode newNode = CreateArea(TeamProjectName, @"Application"); //Add area Application
            PrintNodeInfo(newNode);
            newNode = CreateArea(TeamProjectName, @"WinClient", @"Application"); //Add area Application\WinClient
            PrintNodeInfo(newNode);
            newNode = CreateArea(TeamProjectName, @"WebClient", @"Application"); //Add area Application\WebClient
            PrintNodeInfo(newNode);
            newNode = CreateArea(TeamProjectName, @"AppServer", @"Application"); //Add area Application\AppServer
            PrintNodeInfo(newNode);
            newNode = CreateArea(TeamProjectName, @"Database"); //Add area Database
            PrintNodeInfo(newNode);
            newNode = CreateArea(TeamProjectName, @"Operational", @"Database"); //Add area Database\Operational
            PrintNodeInfo(newNode);

            newNode = RenameClassificationNode(TeamProjectName, @"Database\Operational", @"Report", TreeStructureGroup.Areas); //Rename area Database\Operational to Database\Report
            PrintNodeInfo(newNode);

            DeleteClassificationNode(TeamProjectName, @"Database", @"", TreeStructureGroup.Areas); // Delete area tree Database and move work items to the root team project area
        }

        static void PrintNodeInfo(WorkItemClassificationNode Node)
        {
            Console.WriteLine("{0} name: {1}", (Node.StructureType == TreeNodeStructureType.Area) ? "Area" : "Iteration", Node.Name);

            //get path from url
            string[] pathArray = Node.Url.Split(new string[] { (Node.StructureType == TreeNodeStructureType.Area) ? "/Areas/" : "/Iterations/" },
                StringSplitOptions.RemoveEmptyEntries);
            if (pathArray.Length == 2) Console.WriteLine("Path: " + pathArray[1].Replace('/', '\\'));

            if (Node.Attributes != null)
            {
                Console.WriteLine("Start Date: {0}", (Node.Attributes.ContainsKey("startDate")) ? Node.Attributes["startDate"].ToString() : "none");
                Console.WriteLine("Finish Date: {0}", (Node.Attributes.ContainsKey("finishDate")) ? Node.Attributes["finishDate"].ToString() : "none");
            }
        }

        /// <summary>
        /// Create new iteration
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="IterationName"></param>
        /// <param name="StartDate"></param>
        /// <param name="FinishDate"></param>
        /// <param name="ParentIterationPath"></param>
        /// <returns></returns>
        static WorkItemClassificationNode CreateIteration(string TeamProjectName, string IterationName, DateTime? StartDate = null, DateTime? FinishDate = null, string ParentIterationPath = null)
        {
            WorkItemClassificationNode newIteration = new WorkItemClassificationNode();
            newIteration.Name = IterationName;

            if (StartDate != null && FinishDate != null)
            {
                newIteration.Attributes = new Dictionary<string, object>();
                newIteration.Attributes.Add("startDate", StartDate);
                newIteration.Attributes.Add("finishDate", FinishDate);
            }

            return WitClient.CreateOrUpdateClassificationNodeAsync(newIteration, TeamProjectName, TreeStructureGroup.Iterations, ParentIterationPath).Result;
        }

        /// <summary>
        /// Create new area
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="AreaName"></param>
        /// <param name="ParentAreaPath"></param>
        /// <returns></returns>
        static WorkItemClassificationNode CreateArea(string TeamProjectName, string AreaName, string ParentAreaPath = null)
        {
            WorkItemClassificationNode newArea = new WorkItemClassificationNode();
            newArea.Name = AreaName;

            return WitClient.CreateOrUpdateClassificationNodeAsync(newArea, TeamProjectName, TreeStructureGroup.Areas, ParentAreaPath).Result;
        }

        /// <summary>
        /// Update iteration dates
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="NodePath"></param>
        /// <param name="StartDate"></param>
        /// <param name="FinishDate"></param>
        /// <returns></returns>
        static WorkItemClassificationNode ReplanIteration(string TeamProjectName, string NodePath, DateTime StartDate, DateTime FinishDate)
        {
            WorkItemClassificationNode node = WitClient.GetClassificationNodeAsync(
                TeamProjectName,
                TreeStructureGroup.Iterations,
                NodePath, 4).Result;

            if (node.Attributes == null) node.Attributes = new Dictionary<string, object>();

            if (!node.Attributes.ContainsKey("startDate")) node.Attributes.Add("startDate", StartDate);
            else node.Attributes["startDate"] = StartDate;
            if (!node.Attributes.ContainsKey("finishDate")) node.Attributes.Add("finishDate", FinishDate);
            else node.Attributes["finishDate"] = FinishDate;

            return UpdateClassificationNode(TeamProjectName, node, GetParentNodePath(NodePath, node.Name));
        }

        /// <summary>
        /// Rename area or iteration
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="NodePath"></param>
        /// <param name="NewNodeName"></param>
        /// <param name="treeStructureGroup"></param>
        /// <returns></returns>
        static WorkItemClassificationNode RenameClassificationNode(string TeamProjectName, string NodePath, string NewNodeName, TreeStructureGroup treeStructureGroup)
        {
            WorkItemClassificationNode node = WitClient.GetClassificationNodeAsync(
                TeamProjectName,
                treeStructureGroup,
                NodePath, 4).Result;

            string parentPath = GetParentNodePath(NodePath, node.Name);

            node.Name = NewNodeName;

            return UpdateClassificationNode(TeamProjectName, node, parentPath);
        }

        /// <summary>
        /// Get parent path from node path
        /// </summary>
        /// <param name="NodePath"></param>
        /// <param name="NodeName"></param>
        /// <returns></returns>
        private static string GetParentNodePath(string NodePath, string NodeName)
        {
            if (!NodePath.Contains("\\")) return null;
            return NodePath.Remove(NodePath.Length - NodeName.Length - 1, NodeName.Length + 1);
        }

        /// <summary>
        /// Move area or iteration to new path
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="NodePath"></param>
        /// <param name="NewParentNodePath"></param>
        /// <param name="treeStructureGroup"></param>
        /// <returns></returns>
        static WorkItemClassificationNode MoveClassificationNode(string TeamProjectName, string NodePath, string NewParentNodePath, TreeStructureGroup treeStructureGroup)
        {
            WorkItemClassificationNode node = WitClient.GetClassificationNodeAsync(
                TeamProjectName,
                treeStructureGroup,
                NodePath, 4).Result;

            return UpdateClassificationNode(TeamProjectName, node, NewParentNodePath);
        }

        /// <summary>
        /// Update info for area or iteration
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="Node"></param>
        /// <param name="ParentNodePath"></param>
        /// <returns></returns>
        static WorkItemClassificationNode UpdateClassificationNode(string TeamProjectName, WorkItemClassificationNode Node, string ParentNodePath = null)
        {
            return WitClient.CreateOrUpdateClassificationNodeAsync(Node, TeamProjectName, (Node.StructureType == TreeNodeStructureType.Area) ? 
                TreeStructureGroup.Areas : TreeStructureGroup.Iterations, ParentNodePath).Result;
        }

        /// <summary>
        /// Remove area or iteration
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="NodePath"></param>
        /// <param name="NewNodePath"></param>
        /// <param name="treeStructureGroup"></param>
        static void DeleteClassificationNode(string TeamProjectName, string NodePath, string NewNodePath, TreeStructureGroup treeStructureGroup)
        {
            WorkItemClassificationNode node = WitClient.GetClassificationNodeAsync(
                TeamProjectName,
                treeStructureGroup,
                NewNodePath, 4).Result;

            WitClient.DeleteClassificationNodeAsync(TeamProjectName, treeStructureGroup, NodePath, node.Id).SyncResult();
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

        #region constants for field and link names 
        class RelConstants
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
            public const string AffectsRefStr = "Microsoft.VSTS.Common.Affects.Forward";
            public const string AffectedByRefStr = "Microsoft.VSTS.Common.Affects.Reverse";            
            public const string AttachmentRefStr = "AttachedFile";
            public const string HyperLinkRefStr = "Hyperlink";
            public const string ArtifactLinkRefStr = "ArtifactLink";

            public const string LinkKeyForDict = "<NewLink>"; // key for dictionary to separate a link from fields            
        }

        #endregion
    }
}
