﻿
namespace Microsoft.CosmosDB.PITRWithRestore
{
    using System;
    using System.Configuration;
    
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;

    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.CosmosDB.PITRWithRestore.CosmosDB;

    /// <summary>
    /// Factory class to create instance of document feed observer.
    /// </summary>
    public class DocumentFeedObserverFactory : IChangeFeedObserverFactory
    {
        /// <summary>
        /// CloudStorageAccount instance retrieved after successfully establishing a connection to the specified Blob Storage Account,
        /// into which created and updated documents will be backed up
        /// </summary>
        private CloudStorageAccount StorageAccount;

        /// <summary>
        /// CloudBlobClient instance used to push backups to the specified Blob Storage Account
        /// </summary>
        private CloudBlobClient CloudBlobClient;

        /// <summary>
        /// Instance of the DocumentClient, used to push failed batches of backups to the Cosmos DB collection
        /// tracking failures during the backup process.
        /// </summary>
        private DocumentClient DocumentClient;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="DocumentFeedObserverFactory" /> class.
        /// </summary>
        public DocumentFeedObserverFactory(DocumentClient client)
        {
            // Retrieve the connection string for use with the application. The storage connection string is stored
            // in an environment variable on the machine running the application called storageconnectionstring.
            // If the environment variable is created after the application is launched in a console or with Visual
            // Studio, the shell or application needs to be closed and reloaded to take the environment variable into account.
            string storageConnectionString = ConfigurationManager.AppSettings["BlobStorageConnectionString"];

            this.DocumentClient = client;

            // Ensure the connection string can be parsed.
            if (CloudStorageAccount.TryParse(storageConnectionString, out this.StorageAccount))
            {
                this.CloudBlobClient = this.StorageAccount.CreateCloudBlobClient();
            }
            else
            {
                // Otherwise, let the user know that they need to define the environment variable.
                Console.WriteLine("The connection string for the Blob Storage Account is invalid. ");
                Console.ReadLine();
            }

            string backupFailureDatabaseName = ConfigurationManager.AppSettings["BackupFailureDatabaseName"];
            string backupFailureCollectionName = ConfigurationManager.AppSettings["BackupFailureCollectionName"];
            int backupFailureCollectionThroughput = int.Parse(ConfigurationManager.AppSettings["BackupFailureCollectionThroughput"]);
            string backupFailureCollectionPartitionKey = ConfigurationManager.AppSettings["BackupFailureCollectionPartitionKey"];

            // Create collection to track backup failures
            CosmosDBHelper.CreateCollectionIfNotExistsAsync(
                this.DocumentClient, 
                backupFailureDatabaseName, 
                backupFailureCollectionName, 
                backupFailureCollectionThroughput, 
                backupFailureCollectionPartitionKey).Wait();
        }

        /// <summary>
        /// Creates a document observer instance which receives the modified documents to be backed up in the Azure Blob Storage account.
        /// </summary>
        /// <returns>A new DocumentFeedObserver instance.</returns>
        public IChangeFeedObserver CreateObserver()
        {
            DocumentFeedObserver newObserver = new DocumentFeedObserver(this.CloudBlobClient, this.DocumentClient);
            return newObserver as IChangeFeedObserver;
        }
    }
}
