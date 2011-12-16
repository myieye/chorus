﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Chorus.Utilities;
using Chorus.VcsDrivers.Mercurial;
using NUnit.Framework;
using Palaso.Progress.LogBox;

namespace LibChorus.Tests.VcsDrivers.Mercurial
{
	[TestFixture]
	public class HgResumeTransportTests
	{
		[Test]
		public void Push_SingleResponse_OK()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress {ShowVerbose=true}, progressForTest}))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				var revisionResponse = ApiResponses.Revisions(setup.Repository.GetTip().Number.Hash);
				setup.AddAndCheckinFile("sample2", "second checkin");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddResponse(revisionResponse);
				apiServer.AddResponse(ApiResponses.PushComplete());
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Push_UnknownServerResponse_Fails()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				apiServer.AddResponse(ApiResponses.Custom(HttpStatusCode.ServiceUnavailable));
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation failed"));
			}
		}

		[Test]
		public void Push_SomeServerTimeOuts_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				var revisionResponse = ApiResponses.Revisions(setup.Repository.GetTip().Number.Hash);
				setup.AddAndCheckinFile("sample2", "second checkin");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddTimeOut();
				apiServer.AddResponse(revisionResponse);
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddResponse(ApiResponses.PushComplete());
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Push_TooManyServerTimeouts_Fails()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				var revisionResponse = ApiResponses.Revisions(setup.Repository.GetTip().Number.Hash);
				setup.AddAndCheckinFile("sample2", "second checkin");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddTimeOut();
				apiServer.AddResponse(revisionResponse);
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddResponse(ApiResponses.PushComplete());
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation failed"));
			}
		}

		[Test]
		public void Push_LargeFileSizeBundle_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PushHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				apiServer.Revisions.Add(setup.Repository.GetTip().Number.Hash);

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Push_BadChecksumInOneChunk_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				var revisionResponse = ApiResponses.Revisions(setup.Repository.GetTip().Number.Hash);
				setup.AddAndCheckinFile("sample2", "second checkin");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddResponse(revisionResponse);
				apiServer.AddResponse(ApiResponses.PushAccepted(5));
				apiServer.AddResponse(ApiResponses.PushAccepted(10));
				apiServer.AddResponse(ApiResponses.BadChecksum());
				apiServer.AddResponse(ApiResponses.PushComplete());
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Push_RepeatedBadChecksum_Fail()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				var revisionResponse = ApiResponses.Revisions(setup.Repository.GetTip().Number.Hash);
				setup.AddAndCheckinFile("sample2", "second checkin");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddResponse(revisionResponse);
				apiServer.AddResponse(ApiResponses.BadChecksum());
				apiServer.AddResponse(ApiResponses.BadChecksum());
				apiServer.AddResponse(ApiResponses.BadChecksum());
				apiServer.AddResponse(ApiResponses.BadChecksum());
				apiServer.AddResponse(ApiResponses.BadChecksum());
				apiServer.AddResponse(ApiResponses.PushComplete());
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation failed"));
			}
		}

		[Test]
		public void Push_MultiChunkBundleAndUnBundleFails_Fail()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				var revisionResponse = ApiResponses.Revisions(setup.Repository.GetTip().Number.Hash);
				setup.AddAndCheckinFile("sample2", "second checkin");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddResponse(revisionResponse);
				apiServer.AddResponse(ApiResponses.PushAccepted(1));
				apiServer.AddResponse(ApiResponses.PushAccepted(2));
				apiServer.AddResponse(ApiResponses.Reset());
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation failed"));
			}
		}

		[Test]
		public void Push_RemoteRepoDbNotExistsAndSetsCorrectlyWithRevHash_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash = setup.Repository.GetTip().Number.Hash;
				var revisionResponse = ApiResponses.Revisions(revHash);
				setup.AddAndCheckinFile("sample2", "second checkin");
				var tipHash = setup.Repository.GetTip().Number.Hash;
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddResponse(revisionResponse);
				apiServer.AddResponse(ApiResponses.PushComplete());
				string dbFilePath = Path.Combine(setup.Repository.PathToRepo, "remoteRepo.db");
				Assert.That(File.Exists(dbFilePath), Is.False);
				transport.Push();
				Assert.That(File.Exists(dbFilePath), Is.True);
				string dbContents = File.ReadAllText(dbFilePath).Trim();
				Assert.That(dbContents, Is.EqualTo(apiServer.Identifier + "|" + tipHash));
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Push_2PushesAndRemoteRepoDbFileUpdated_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				string dbFilePath = Path.Combine(setup.Repository.PathToRepo, "remoteRepo.db");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);

				// first push
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash1 = setup.Repository.GetTip().Number.Hash;
				setup.AddAndCheckinFile("sample2", "second checkin");
				string tipHash = setup.Repository.GetTip().Number.Hash;
				var revisionResponse = ApiResponses.Revisions(revHash1);
				apiServer.AddResponse(revisionResponse);
				apiServer.AddResponse(ApiResponses.PushComplete());
				transport.Push();
				string dbContents = File.ReadAllText(dbFilePath).Trim();
				Assert.That(dbContents, Is.EqualTo(apiServer.Identifier + "|" + tipHash));
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));

				// second push
				setup.AddAndCheckinFile("sample3", "third checkin");

				setup.AddAndCheckinFile("sample4", "fourth checkin");
				string tipHash2 = setup.Repository.GetTip().Number.Hash;
				apiServer.AddResponse(ApiResponses.PushAccepted(1));
				apiServer.AddResponse(ApiResponses.PushComplete());
				transport.Push();
				dbContents = File.ReadAllText(dbFilePath).Trim();
				Assert.That(dbContents, Is.EqualTo(apiServer.Identifier + "|" + tipHash2));
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Push_2DifferentApiServers_HgRepoFileUpdatedWithBothEntries()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer1 = new DummyApiServerForTest("apiServer1"))
			using (var apiServer2 = new DummyApiServerForTest("apiServer2"))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				string dbFilePath = Path.Combine(setup.Repository.PathToRepo, "remoteRepo.db");

				var transport1 = new HgResumeTransport(setup.Repository, "test repo", apiServer1, progress);
				// first push to apiServer1
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash1 = setup.Repository.GetTip().Number.Hash;
				setup.AddAndCheckinFile("sample2", "second checkin");
				string tipHash1 = setup.Repository.GetTip().Number.Hash;
				var revisionResponse = ApiResponses.Revisions(revHash1);
				apiServer1.AddResponse(revisionResponse);
				apiServer1.AddResponse(ApiResponses.PushComplete());
				transport1.Push();

				// first push to apiServer2
				var transport2 = new HgResumeTransport(setup.Repository, "test repo", apiServer2, progress);
				apiServer2.AddResponse(revisionResponse);
				apiServer2.AddResponse(ApiResponses.PushComplete());
				transport2.Push();

				// check contents of remoteRepoDb
				string[] dbContents = File.ReadAllLines(dbFilePath);
				Assert.That(dbContents, Contains.Item(apiServer1.Identifier + "|" + tipHash1));
				Assert.That(dbContents, Contains.Item(apiServer2.Identifier + "|" + tipHash1));

				// second push
				setup.AddAndCheckinFile("sample3", "third checkin");
				setup.AddAndCheckinFile("sample4", "fourth checkin");
				string tipHash2 = setup.Repository.GetTip().Number.Hash;
				apiServer1.AddResponse(ApiResponses.PushAccepted(1));
				apiServer1.AddResponse(ApiResponses.PushComplete());
				transport1.Push();

				dbContents = File.ReadAllLines(dbFilePath);
				Assert.That(dbContents, Contains.Item(apiServer1.Identifier + "|" + tipHash2));
				Assert.That(dbContents, Contains.Item(apiServer2.Identifier + "|" + tipHash1));

				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Pull_UnknownServerResponse_Fails()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				setup.AddAndCheckinFile("sample1", "first checkin");
				apiServer.AddResponse(ApiResponses.Custom(HttpStatusCode.ServiceUnavailable));
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation failed"));
			}
		}

		[Test]
		public void Pull_ServerTimeOut_Fails()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				setup.AddAndCheckinFile("sample1", "first checkin");
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				apiServer.AddTimeOut();
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation failed"));
			}
		}

		[Test]
		public void Pull_BundleInOneChunk_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PullHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash = setup.Repository.GetTip().Number.Hash;
				setup.AddAndCheckinFile("sample2", "second checkin");
				apiServer.PrepareBundle(revHash);
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation completed successfully"));
			}
		}

		[Test]
		public void Pull_BundleInMultipleChunks_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PullHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash = setup.Repository.GetTip().Number.Hash;

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);

				apiServer.PrepareBundle(revHash);

				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation completed successfully"));
			}
		}

		[Test]
		public void Pull_SomeTimeOuts_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PullHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash = setup.Repository.GetTip().Number.Hash;

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);

				apiServer.PrepareBundle(revHash);

				apiServer.AddTimeoutResponse(2);
				apiServer.AddTimeoutResponse(3);
				apiServer.AddTimeoutResponse(6);
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation completed successfully"));
			}
		}

		[Test]
		public void Pull_SomeBadChecksums_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PullHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash = setup.Repository.GetTip().Number.Hash;

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);

				apiServer.PrepareBundle(revHash);

				apiServer.AddBadChecksumResponse(2);
				apiServer.AddBadChecksumResponse(3);
				apiServer.AddBadChecksumResponse(6);
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation completed successfully"));
			}
		}

		[Test]
		public void Pull_ChunkSizeReducedWithTimeouts_Success()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PullHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash = setup.Repository.GetTip().Number.Hash;

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);

				apiServer.PrepareBundle(revHash);

				apiServer.AddTimeoutResponse(2);
				apiServer.AddTimeoutResponse(3);
				apiServer.AddTimeoutResponse(6);
				transport.Pull();

				Assert.That(progressForTest.AllMessages, Contains.Item("Received 0+10000 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Received 10000+2500 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Received 15000+1250 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation completed successfully"));
			}
		}

		[Test]
		public void Pull_PullOperationFailsMidwayAndStartsAgainWithSeparatePull_Resumes()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PullHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash = setup.Repository.GetTip().Number.Hash;

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
															String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll",
																		  Path.DirectorySeparatorChar));
				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);
				apiServer.PrepareBundle(revHash);
				apiServer.AddTimeoutResponse(4);
				apiServer.AddTimeoutResponse(5);
				apiServer.AddTimeoutResponse(6);
				apiServer.AddTimeoutResponse(7);
				apiServer.AddTimeoutResponse(8);
				apiServer.AddTimeoutResponse(12);
				apiServer.AddTimeoutResponse(13);
				apiServer.AddTimeoutResponse(14);
				apiServer.AddTimeoutResponse(15);
				apiServer.AddTimeoutResponse(16);

				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation failed"));

				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Resuming pull operation at 30000 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation failed"));

				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Resuming pull operation at 60000 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation completed successfully"));
			}
		}

		[Test]
		public void Push_PushOperationFailsMidwayAndBeginsAgainWithAdditionalPush_Resumes()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PushHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				apiServer.Revisions.Add(setup.Repository.GetTip().Number.Hash);

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddTimeoutResponse(4);
				apiServer.AddTimeoutResponse(5);
				apiServer.AddTimeoutResponse(6);
				apiServer.AddTimeoutResponse(7);
				apiServer.AddTimeoutResponse(8);
				apiServer.AddTimeoutResponse(12);
				apiServer.AddTimeoutResponse(13);
				apiServer.AddTimeoutResponse(14);
				apiServer.AddTimeoutResponse(15);
				apiServer.AddTimeoutResponse(16);
				apiServer.AddTimeoutResponse(19);
				apiServer.AddTimeoutResponse(20);
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation failed"));
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Resuming push operation at 30000 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation failed"));
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Resuming push operation at 50000 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Push_PushFailsMidwayThenRepoChanges_PushDoesNotResume()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PushHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				apiServer.Revisions.Add(setup.Repository.GetTip().Number.Hash);

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				apiServer.AddTimeoutResponse(4);
				apiServer.AddTimeoutResponse(5);
				apiServer.AddTimeoutResponse(6);
				apiServer.AddTimeoutResponse(7);
				apiServer.AddTimeoutResponse(8);
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation failed"));

				setup.AddAndCheckinFile("sample2", "second checkin");
				transport.Push();
				Assert.That(progressForTest.AllMessages, Has.No.Member("Resuming push operation at 30000 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Push operation completed successfully"));
			}
		}

		[Test]
		public void Pull_PullFailsMidwayTheRemoteRepoChanges_PullFinishesThenStartsSecondPullToGetNewChanges()
		{
			var progressForTest = new ProgressForTest();
			using (var localSetup = new RepositorySetup("hgresumetestlocal"))
			using (var remoteSetup = new RepositorySetup("hgresumetestremote", localSetup))
			using (var apiServer = new PullHandlerApiServerForTest(remoteSetup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(localSetup.Repository, "test repo", apiServer, progress);
				localSetup.AddAndCheckinFile("sample1", "first checkin");
				remoteSetup.Repository.Pull("localRepo", localSetup.Repository.PathToRepo);
				remoteSetup.Repository.Update();
				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
															String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll",
																		  Path.DirectorySeparatorChar));
				string largeFilePath = remoteSetup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				remoteSetup.Repository.AddAndCheckinFile(largeFilePath);

				string localTip = localSetup.Repository.GetTip().Number.Hash;
				string remoteTip = remoteSetup.Repository.GetTip().Number.Hash;

				apiServer.PrepareBundle(localTip);
				apiServer.Revisions.Add(localTip);
				apiServer.Revisions.Add(remoteTip);
				apiServer.AddTimeoutResponse(4);
				apiServer.AddTimeoutResponse(5);
				apiServer.AddTimeoutResponse(6);
				apiServer.AddTimeoutResponse(7);
				apiServer.AddTimeoutResponse(8);

				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation failed"));

				remoteSetup.AddAndCheckinFile("sample2", "second checkin");
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Resuming pull operation at 30000 bytes"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Pull operation completed successfully"));
				Assert.That(progressForTest.AllMessages, Contains.Item("Remote repo has changed.  Initiating additional pull operation"));
				IEnumerable<string> msgs = progressForTest.Messages.Where(x => x == "Pull operation completed successfully");
				Assert.That(msgs.ToList(), Has.Count.EqualTo(2));

			}
		}

		[Test]
		public void Push_ServerNotAvailableMidTransaction_NotAvailableMessage()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PushHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				apiServer.Revisions.Add(setup.Repository.GetTip().Number.Hash);
				string serverMessage = "The server is down for scheduled maintenance";
				apiServer.AddServerUnavailableResponse(7, serverMessage);

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Server temporarily unavailable: " + serverMessage));
				Assert.That(progressForTest.AllMessages, Has.No.Member("Push operation completed successfully"));
			}
		}

		[Test]
		public void Pull_ServerNotAvailableMidTransaction_NotAvailableMessage()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new PullHandlerApiServerForTest(setup.Repository))
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				setup.AddAndCheckinFile("sample1", "first checkin");
				string revHash = setup.Repository.GetTip().Number.Hash;

				// just pick a file larger than 10K for use as a test... any file will do
				string sourcePathOfLargeFile = Path.Combine(ExecutionEnvironment.DirectoryOfExecutingAssembly,
					String.Format("..{0}..{0}lib{0}Debug{0}Palaso.Tests.dll", Path.DirectorySeparatorChar));

				string largeFilePath = setup.ProjectFolder.GetNewTempFile(false).Path;
				File.Copy(sourcePathOfLargeFile, largeFilePath);
				setup.Repository.AddAndCheckinFile(largeFilePath);

				apiServer.PrepareBundle(revHash);

				apiServer.AddTimeoutResponse(2);
				apiServer.AddTimeoutResponse(3);
				apiServer.AddTimeoutResponse(6);
				string serverMessage = "The server is down for scheduled maintenance";
				apiServer.AddServerUnavailableResponse(7, serverMessage);
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Server temporarily unavailable: " + serverMessage));
				Assert.That(progressForTest.AllMessages, Has.No.Member("Pull operation completed successfully"));
			}
		}

		[Test]
		public void Pull_InitialServerResponseServerNotAvailable_NotAvailableMessage()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				string serverMessage = "The server is down for scheduled maintenance";
				apiServer.AddResponse(ApiResponses.NotAvailable(serverMessage));
				apiServer.AddResponse(ApiResponses.PullNoChange());
				transport.Pull();
				Assert.That(progressForTest.AllMessages, Contains.Item("Server temporarily unavailable: " + serverMessage));
				Assert.That(progressForTest.AllMessages, Has.No.Member("The pull operation completed successfully"));
			}
		}

		[Test]
		public void Push_InitialServerResponseServerNotAvailable_NotAvailableMessage()
		{
			var progressForTest = new ProgressForTest();
			using (var setup = new RepositorySetup("hgresumetest"))
			using (var apiServer = new DummyApiServerForTest())
			using (var progress = new MultiProgress(new IProgress[] { new ConsoleProgress { ShowVerbose = true }, progressForTest }))
			{
				setup.AddAndCheckinFile("sample1", "first checkin");
				var transport = new HgResumeTransport(setup.Repository, "test repo", apiServer, progress);
				string serverMessage = "The server is down for scheduled maintenance";
				apiServer.AddResponse(ApiResponses.NotAvailable(serverMessage));
				apiServer.AddResponse(ApiResponses.PushComplete());
				transport.Push();
				Assert.That(progressForTest.AllMessages, Contains.Item("Server temporarily unavailable: " + serverMessage));
				Assert.That(progressForTest.AllMessages, Has.No.Member("The push operation completed successfully"));
			}
		}
	}
}
