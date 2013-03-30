namespace SemaphoreLock
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Threading;
	using System.Threading.Tasks;

	internal class Program
	{
		private static void Main()
		{
			var file = new DownloadableFile();

			using (var stream = File.Create("Output.txt"))
			{
				DownloadFileVersion2(file, stream);
			}
		}

		private static readonly object _writeLock = new object();

		private static void DownloadFileVersion1(DownloadableFile file, FileStream destination)
		{
			var tasks = new List<Task>();

			foreach (int i in Enumerable.Range(0, file.ChunkCount))
			{
				int chunkIndex = i;

				tasks.Add(Task.Run(async delegate
				{
					var chunkContents = await new HttpClient().GetByteArrayAsync(file.GetChunkUrl(chunkIndex));

					lock (_writeLock)
						destination.Write(chunkContents, 0, chunkContents.Length);
				}));
			}

			Task.WaitAll(tasks.ToArray());
		}

		private static readonly SemaphoreSlim _concurrentDownloadSemaphore = new SemaphoreSlim(3);

		private static void DownloadFileVersion2(DownloadableFile file, FileStream destination)
		{
			var tasks = new List<Task>();

			foreach (int i in Enumerable.Range(0, file.ChunkCount))
			{
				int chunkIndex = i;

				tasks.Add(Task.Run(async delegate
				{
					byte[] chunkContents;

					await _concurrentDownloadSemaphore.WaitAsync();

					try
					{
						chunkContents = await new HttpClient().GetByteArrayAsync(file.GetChunkUrl(chunkIndex));
					}
					finally
					{
						_concurrentDownloadSemaphore.Release();
					}

					lock (_writeLock)
						destination.Write(chunkContents, 0, chunkContents.Length);
				}));
			}

			Task.WaitAll(tasks.ToArray());
		}

		private sealed class DownloadableFile
		{
			public readonly int ChunkCount = 10;

			public Uri GetChunkUrl(int chunkIndex)
			{
				return new Uri("http://google.com");
			}
		}
	}
}