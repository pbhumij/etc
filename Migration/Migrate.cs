using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Containers;
using InMemoryStructure;

namespace Migration
{
    public class Migrate
    {
        private static Object thisLock = new object();
        private ICloud cloud = null;
        private ICloud local = null;
        private List<Containers.File> downloadList = null;
        private Containers.Directory rootDirectory = null;
        private Queue<Containers.Directory> fileQueue = null;
        private TransactionLogFile transactionLog = null;

        public Migrate(ICloud cloud, ICloud local)
        {
            this.cloud = cloud;
            this.local = local;
            transactionLog = new TransactionLogFile();
            fileQueue = new Queue<Containers.Directory>();
            downloadList = new List<Containers.File>();
        }

        public async Task StartMigration()
        {
            try
            {
                if (!IsExistTransactionLogFile())
                {
                    await GetRootFromSource();
                    if(rootDirectory != null)
                    {
                        await CopyFilesFromCloudToLocal();
                        await cloud.DeleteAllFiles("root");
                        await Task.Delay(10000);
                        await RevertToCloudFromLocal();
                    }                    
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                throw new Exception(e.Message);
            }
        }

        private async Task RevertToCloudFromLocal()
        {
            DeserializeTransactioFile();
            fileQueue.Enqueue(rootDirectory);
            UploadToCloud();
            DeleteLogFile();
        }

        private void UploadToCloud()
        {
            if (fileQueue.Count == 0)
                return;
            while(fileQueue.Count > 0)
            {
                var currentDirectory = fileQueue.Dequeue();
                if(currentDirectory.SubDirectories != null)
                {
                    foreach(var item in currentDirectory.SubDirectories)
                    {
                        cloud.UploadDirectory(item);
                        fileQueue.Enqueue(item);
                    }
                }
                if (currentDirectory.Files != null)
                {
                    foreach (var item in currentDirectory.Files)
                    {
                        Task<MemoryStream> task = Task.Run(() => local.Download(item));
                        MemoryStream stream = task.Result;
                        cloud.UploadFile(item, stream);
                    }
                }
            }
            UploadToCloud();
        }

        private async Task GetRootFromSource()
        {
            ListResource files = new ListResource(cloud);
            rootDirectory = await files.GetFiles();
        }

        // The transaction log file represents a list of all downloadable objects. It is not hierachical
        private void CreateTransactionLogFile()
        {
            DataContractJsonSerializer serialize = new DataContractJsonSerializer(typeof(Containers.Directory));
            lock (thisLock)
            {
                using (FileStream stream = new FileStream(transactionLog.path, FileMode.Create))
                {
                    serialize.WriteObject(stream, rootDirectory);
                    stream.Flush();
                }
            }
        }

        private async Task CopyFilesFromCloudToLocal(bool resume = false)
        {
            try
            {
                if (resume)
                {
                    DeserializeTransactioFile();
                    Copy();
                }
                else
                {
                    CreateTransactionLogFile();
                    fileQueue.Enqueue(rootDirectory);
                    Copy();
                }
                fileQueue.Clear();
                rootDirectory = null;
            }
            catch (Exception e)
            {
                 throw e;
            }
        }

        private void Copy()
        {
            if (fileQueue.Count == 0)
                return;
            while(fileQueue.Count > 0)
            {
                var currentDirectory = fileQueue.Dequeue();
                if(currentDirectory.SubDirectories != null)
                {
                    foreach(var item in currentDirectory.SubDirectories)
                    {
                        local.UploadDirectory(item);
                        fileQueue.Enqueue(item);
                    }
                }
                if(currentDirectory.Files != null)
                {
                    foreach (var item in currentDirectory.Files)
                    {
                        Task<MemoryStream> task = Task.Run(() => cloud.Download(item));
                        MemoryStream stream = task.Result;
                        local.UploadFile(item, stream);
                    }
                }
            }
            Copy();
        }

        private void UpdateTransactionFile(Containers.File item)
        {
            DataContractJsonSerializer serialize = new DataContractJsonSerializer(typeof(List<Containers.File>));
            lock (thisLock)
            {
                using (FileStream stream = new FileStream(transactionLog.path, FileMode.Open))
                {
                    serialize.WriteObject(stream, downloadList);
                    stream.Flush();
                }
            }
        }


        private void DeleteLogFile()
        {
            if (IsCopySuccess())
            {
                System.IO.File.Delete(transactionLog.path);
            }
            else
            {
                //CleanTransactionFile();
            }
        }    

        private bool IsExistTransactionLogFile()
        {
            if (System.IO.File.Exists(transactionLog.path))
                return true;
            else
                return false;
        }


        private bool IsCopySuccess()
        {
            bool success = true;
            foreach(var item in downloadList)
            {
                if (item != null && !(item.migrateStatus == "Downloaded"))
                {
                    success = false;
                    break;
                }
            }
            return success;
        }

        private bool CopyDirectory(Containers.File item)
        {
            try
            {
               
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        // Deserialize transaction file and assign the returned object to global variable CloudItems
        private bool DeserializeTransactioFile()
        {
            try
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Containers.Directory));
                lock (thisLock)
                {
                    using (FileStream stream = new FileStream(transactionLog.path, FileMode.Open))
                    {
                        stream.Position = 0;
                        rootDirectory = (Containers.Directory)ser.ReadObject(stream);
                    }
                }
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }    
       
    }
}
