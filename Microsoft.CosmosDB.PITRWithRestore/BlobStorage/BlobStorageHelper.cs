﻿
namespace Microsoft.CosmosDB.PITRWithRestore.BlobStorage
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    
    public class BlobStorageHelper
    {
        /// <summary>
        /// Writes a compressed byte array to a Blob Storage container
        /// </summary>
        /// <param name="blockBlob">Blob to backup</param>
        /// <param name="compressedByteArray">Compressed byte array containing compressed Cosmos DB documents to be backed up</param>
        /// <param name="maxRetriesOnRateLimitedWritesToBlobAccount">Maximum number of retries when writes to the Blob Storage account fail</param>
        /// <returns></returns>
        public static bool WriteToBlobStorage(CloudBlockBlob blockBlob, byte[] compressedByteArray, int maxRetriesOnRateLimitedWritesToBlobAccount)
        {
            bool writeToBlobSucceeded = false;

            try
            {
                blockBlob.UploadFromByteArray(compressedByteArray, 0, compressedByteArray.Length);
                writeToBlobSucceeded = true;
                Console.WriteLine("Successfully wrote blob to Storage Account");
            }
            catch (StorageException ex)
            {
                int exceptionStatusCode = ex.RequestInformation.HttpStatusCode;

                // Throttling exception - implement custom exponential backoff retry logic for 10 retries
                if (exceptionStatusCode == 500 || exceptionStatusCode == 503)
                {
                    int retryWaitTime = 1;
                    int retryCount = 1;
                    
                    // Custom exponential backoff-retry logic when rate limited by Azure Blob Storage
                    while (!writeToBlobSucceeded && retryCount <= maxRetriesOnRateLimitedWritesToBlobAccount)
                    {
                        Thread.Sleep(retryWaitTime * 1000);
                        try
                        {
                            blockBlob.UploadFromByteArray(compressedByteArray, 0, compressedByteArray.Length);
                            writeToBlobSucceeded = true;
                        }
                        catch (StorageException ie)
                        {
                            // Throttling exception - continue with custom exponential backoff retry logic for 10 retries
                            if (ie.RequestInformation.HttpStatusCode == 500 || ie.RequestInformation.HttpStatusCode == 503)
                            {
                                retryWaitTime *= 2;
                            }
                        }
                        catch (Exception iex)
                        {
                            retryWaitTime *= 2;
                            retryCount++;
                        }
                        finally
                        {
                            retryCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                writeToBlobSucceeded = false;
            }

            return writeToBlobSucceeded;
        }

        /// <summary>
        /// Fetches the list of Blob Storage containers with blobs to be restored
        /// </summary>
        /// <param name="cloudBlobClient">>CloudBlobClient to interact with the Blob Storage Account</param>
        /// <returns></returns>
        public static List<string> GetListOfContainersInStorageAccount(CloudBlobClient cloudBlobClient)
        {
            Console.WriteLine("Fetching list of container names");

            List<string> containerNames = new List<string>();
            foreach (CloudBlobContainer eachContainer in cloudBlobClient.ListContainers())
            {
                if (eachContainer.Name.StartsWith("backup"))
                {
                    Console.WriteLine("Found backup container: {0}", eachContainer.Name);
                    containerNames.Add(eachContainer.Name);
                }
            }

            return containerNames;
        }

        /// <summary>
        /// Iterates over each blob file across all backup containers in the Blob Storage Account to calculate
        /// the number of Cosmos DB documents backed up
        /// </summary>
        /// <param name="cloudBlobClient">CloudBlobClient to interact with the Blob Storage Account</param>
        /// <returns></returns>
        public static int GetListOfDocumentsBackedUpInContainer(CloudBlobClient cloudBlobClient)
        {
            int numDocumentsBackedUpInBlobStorageAccount = 0;

            foreach (CloudBlobContainer eachContainer in cloudBlobClient.ListContainers())
            {
                if (eachContainer.Name.StartsWith("backup"))
                {
                    Console.WriteLine("Found backup container: {0}", eachContainer.Name);

                    CloudBlobContainer blobContainer = cloudBlobClient.GetContainerReference(eachContainer.Name);
                    foreach (IListBlobItem blobItem in blobContainer.ListBlobs())
                    {
                        string blobName = ((CloudBlockBlob)blobItem).Name;
                        string[] blobNameComponents = blobName.Split('-');
                        
                        numDocumentsBackedUpInBlobStorageAccount += int.Parse(blobNameComponents[2]);
                    }
                }
            }

            return numDocumentsBackedUpInBlobStorageAccount;
        }
    }
}
