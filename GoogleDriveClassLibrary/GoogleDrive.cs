using Containers;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace GoogleDriveClassLibrary
{
    /// <summary>
    /// Google Drive specific collection of methods.  
    /// </summary>
    public class GoogleDrive : ICloud
    {
        public string sourceFolder { get; set; }
        /// <summary>
        /// This Method takes in a parent directory as parameter.
        /// Lists all the sub-directories and adds them to the parent directory. 
        /// Lists all the files and adds them to the parent directory. 
        /// </summary>

        public GoogleDrive(string folderId)
        {
            this.sourceFolder = sourceFolder;
        }

        public GoogleDrive()
        {

        }

        public async Task ListFilesAsync(Containers.Directory directory)
        {
            using (DriveService service = DriveClient.GetInfo())
            {
                try
                {
                    FilesResource.ListRequest listRequest = service.Files.List();
                    listRequest.Q = "'" + directory.id + "' in parents and trashed = false";
                    listRequest.OrderBy = "name";
                    listRequest.Fields = "nextPageToken, files" +
                        "(id, name, mimeType, permissions, webContentLink, description, size, shared, owners, parents)";
                    Google.Apis.Drive.v3.Data.FileList ListResponse = listRequest.Execute();
                    while (ListResponse.Files != null && ListResponse.Files.Count != 0)
                    {
                        List<Task> tasks = new List<Task>();
                        foreach (var item in ListResponse.Files)
                        {
                            tasks.Add(Task.Run(() => AddToParentDirectoryObject(item, directory)));
                        }
                        Task.WaitAll(tasks.ToArray());
                        if (ListResponse.NextPageToken == null)
                            break;
                        listRequest.PageToken = ListResponse.NextPageToken;
                        ListResponse = listRequest.Execute();
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(e.StackTrace);
                }
            }

        }

        // Returns the Google Drive provided specific id of the root directory.
        public Containers.Directory GetRoot()
        {
            Containers.Directory root = new Containers.Directory()
            {
                id = "root",
            };
            return root;
        }

        // Checks whether a file is of directory type.
        internal bool isDirectory(String FileMimeType)
        {
            if (FileMimeType.Equals("application/vnd.google-apps.folder"))
                return true;
            else
                return false;
        }

        internal void AddToParentDirectoryObject(Google.Apis.Drive.v3.Data.File item, Containers.Directory directory)
        {
            if (isDirectory(item.MimeType))
            {
                if (directory.SubDirectories == null)
                    directory.SubDirectories = new List<Containers.Directory>();
                directory.SubDirectories.Add(CreateDirectoryObject(item, directory));
            }
            else
            {
                if (directory.Files == null)
                    directory.Files = new List<Containers.File>();
                directory.Files.Add(CreateFileObject(item, directory));
            }
        }
        internal Containers.Directory CreateDirectoryObject(Google.Apis.Drive.v3.Data.File item, Containers.Directory parentDirectory)
        {
            Containers.Directory directory = new Containers.Directory()
            {
                id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Size = item.Size,
                SharedLink = GetSharedLink(item),
                Owners = GetOwners(item),
                CreationTime = item.CreatedTime.ToString(),
                Users = GetUsers(item),
                path = parentDirectory.path + parentDirectory.Name + "\\",
                mimeType = "application/folder",
                Parent = (List<string>)item.Parents,
            };
            return directory;
        }

        internal Containers.File CreateFileObject(Google.Apis.Drive.v3.Data.File item, Containers.Directory parentDirectory)
        {
            Containers.File file = new Containers.File()
            {
                id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Size = item.Size,
                SharedLink = item.WebContentLink,
                Owners = GetOwners(item),
                CreationTime = item.CreatedTime.ToString(),
                Users = GetUsers(item),
                path = parentDirectory.path + parentDirectory.Name + "\\",
                mimeType = item.MimeType,
                Parent = (List<string>)item.Parents,
            };
            return file;
        }

        internal String GetSharedLink(Google.Apis.Drive.v3.Data.File item)
        {
            if ((bool)item.Shared)
            {
                var url = item.WebContentLink;
                return url;
            }
            else
            {
                return null;
            }
        }

        internal List<Owner> GetOwners(Google.Apis.Drive.v3.Data.File item)
        {
            List<Owner> owners = new List<Owner>();
            foreach (var owner in item.Owners)
            {
                Containers.Owner newOwner = new Containers.Owner()
                {
                    DisplayName = owner.DisplayName,
                    Email = owner.EmailAddress,
                };
                owners.Add(newOwner);
            }
            return owners;
        }

        /// <summary>
        /// This method takes a Google Drive file or folder as parameter
        /// and returns a list of users along with their access permissions type.
        /// </summary>
        internal List<Containers.User> GetUsers(Google.Apis.Drive.v3.Data.File file)
        {
            List<Containers.User> Users = null;
            // Add users and permissions
            IList<Google.Apis.Drive.v3.Data.Permission> permissions = file.Permissions;
            if (permissions != null && permissions.Count > 0)
            {
                Users = new List<Containers.User>();
                foreach (Google.Apis.Drive.v3.Data.Permission permission in permissions)
                {
                    Containers.User user = CreateUserObject(permission);
                    if (user != null)
                        Users.Add(user);
                }
            }
            return Users;
        }

        internal Containers.User CreateUserObject(Google.Apis.Drive.v3.Data.Permission permission)
        {
            Containers.User user = null;
            if (permission.DisplayName != null)
            {
                user = new Containers.User()
                {
                    DisplayName = permission.DisplayName,
                    EmailAddress = permission.EmailAddress,
                    Permissions = GetPermissions(permission),
                };
            }
            return user;
        }

        internal List<Containers.Permission> GetPermissions(Google.Apis.Drive.v3.Data.Permission permission)
        {
            List<Containers.Permission> permissions = new List<Containers.Permission>();
            Containers.Permission userPermission = new Containers.Permission();
            userPermission.PermissionType = permission.Role.ToString();
            permissions.Add(userPermission);
            return permissions;
        }

        public async Task ResumableChunkUpload(string filePath)
        {
            using (var client = DriveClient.GetInfo())
            {
                try
                {
                    FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    FilesResource.CreateMediaUpload request = new FilesResource.CreateMediaUpload(client,
                        new Google.Apis.Drive.v3.Data.File
                        {
                            Name = "new File",
                            Parents = new List<string>() { "1K46j8qE2sOiFAMNvSz21xFxgpVTYlcWm" },
                        },
                        stream,
                        "video/mpeg4");
                    request.ChunkSize = 1024 * 1024;
                    await request.InitiateSessionAsync();
                    Task<Google.Apis.Upload.IUploadProgress> uploadTask = request.UploadAsync();
                    for (var i = 0; i < 1000; i++)
                    {
                        System.Threading.Thread.Sleep(1000);
                        Console.WriteLine(request.GetProgress().BytesSent);
                    }
                    await uploadTask;
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + " " + e.StackTrace);
                }
            }
        }

        public MemoryStream Download(Containers.File item)
        {
            using (var client = DriveClient.GetInfo())
            {               
                MemoryStream stream = new MemoryStream();
                var downloadRequest = client.Files.Get(item.id);
                Task download = Task.Run(() => downloadRequest.DownloadAsync(stream));
                download.Wait();
                return stream;
            }
        }

        private string getDirectoryPath(string downloadPath)
        {
            string path = downloadPath.Substring(0,(downloadPath.LastIndexOf('\\')));
            return path;
        }

        public void createTestFolders()
        {
            using (var client = DriveClient.GetInfo())
            {
                for (var i = 0; i < 10000; i++)
                {
                    var fileMetadata = new Google.Apis.Drive.v3.Data.File
                    {
                        Name = "abc"+i.ToString(),
                        MimeType = "application/vnd.google-apps.folder"
                    };
                    var request = client.Files.Create(fileMetadata);
                    request.Execute();
                }
            }
        }

        public void UploadFile(Containers.File item, MemoryStream stream = null)
        {
           
           
                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = item.Name,
                    Parents = item.Parent,
                };
                FilesResource.CreateMediaUpload request;
                stream.Position = 0;
                using (var service = DriveClient.GetInfo())
                {
                    request = service.Files.Create(
                    fileMetadata, stream, item.mimeType);
                    request.Upload();
                }
                var file = request.ResponseBody;
            
            
        }

        public void UploadDirectory(Containers.Directory item)
        {
            try
            {
                using (var service = DriveClient.GetInfo())
                {
                    var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                    {
                        Name = item.Name,
                        MimeType = "application/vnd.google-apps.folder",
                        Parents = item.Parent,
                    };
                    var request = service.Files.Create(fileMetadata).Execute();
                }
            }
            catch(Exception e)
            {
                throw e;
            }
        }

        public async Task DeleteAllFiles(string fileId)
        {
            using (var client = DriveClient.GetInfo())
            {
                FilesResource.ListRequest listRequest = client.Files.List();
                listRequest.Q = "'" + fileId + "' in parents and trashed = false";
                listRequest.OrderBy = "name";
                //listRequest.Fields = "nextPageToken, files" +
                //    "(id, name, mimeType, permissions, webContentLink, description, size, shared, owners, parents)";
                Google.Apis.Drive.v3.Data.FileList ListResponse = listRequest.Execute();
                while (ListResponse.Files != null)
                {
                    List<Task> tasks = new List<Task>();
                    foreach (var item in ListResponse.Files)
                    {
                        tasks.Add(Task.Run(() => client.Files.Delete(item.Id).Execute()));
                    }
                    Task.WaitAll(tasks.ToArray());
                    if (ListResponse.NextPageToken == null)
                        break;
                    listRequest.PageToken = ListResponse.NextPageToken;
                    ListResponse = listRequest.Execute();
                }
            }
        }

        public long GetTotalSpace()
        {
            using (var client = DriveClient.GetInfo())
            {
                Google.Apis.Drive.v3.AboutResource.GetRequest request = client.About.Get();
                request.Fields = "storageQuota";
                var response = request.Execute();
                return (long)response.StorageQuota.Limit;
            }
        }

        public long GetUsedSpace()
        {
            using (var client = DriveClient.GetInfo())
            {
                Google.Apis.Drive.v3.AboutResource.GetRequest request = client.About.Get();
                request.Fields = "storageQuota";
                var response = request.Execute();
                return (long)response.StorageQuota.UsageInDrive;
            }
        }
    }
}

