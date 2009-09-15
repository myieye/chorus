using System;
using System.IO;
using System.Text;
using System.Xml;
using Chorus.merge;
using Chorus.merge.xml.lift;
using Chorus.sync;
using Chorus.Utilities;
using Chorus.VcsDrivers.Mercurial;
using LibChorus.Tests.merge;
using NUnit.Framework;

namespace LibChorus.Tests.sync
{
	/// <summary>
	/// I don't know what to call this.... it's about what happens when things go bad, want to make
	/// sure nothing is lost.
	/// </summary>
	[TestFixture]
	public class SynchronizerBadSituationTests
	{

		[Test]//regression
		public void RepoProjectName_SourceHasDotInName_IsNotLost()
		{
			using (TempFolder f = new TempFolder("SourceHasDotInName_IsNotLost.x.y"))
			{
				Synchronizer m = new Synchronizer(f.Path, new ProjectFolderConfiguration("blah"), new ConsoleProgress());

				Assert.AreEqual("SourceHasDotInName_IsNotLost.x.y", m.RepoProjectName);
			}
		}


		[Test]
		public void Sync_ExceptionInMergeCode_LeftWith2HeadsAndErrorOutputToProgress()
		{
			using (RepositoryWithFilesSetup bob = RepositoryWithFilesSetup.CreateWithLiftFile("bob"))
			{
				using (RepositoryWithFilesSetup sally = RepositoryWithFilesSetup.CreateByCloning("sally", bob))
				{
					bob.ReplaceSomething("bobWasHere");
					bob.AddAndCheckIn();
					sally.ReplaceSomething("sallyWasHere");
					using (new FailureSimulator("LiftMerger.FindEntryById"))
					{
						sally.CheckinAndPullAndMerge(bob);
					}
					Assert.IsTrue(sally.ProgressString.Contains("InduceChorusFailure"));

				   sally.AssertHeadCount(2);
					//ok, Bob's the tip, but...
					Assert.AreEqual("bob", sally.Repository.GetTip().UserId);
					//make sure we didn't move up to that tip, because we weren't able to merge with it
					var currentRevision = sally.GetRepository().GetRevisionWorkingSetIsBasedOn();
					Assert.AreEqual("sally",  sally.GetRepository().GetRevision(currentRevision.Number.Hash).UserId);
					Assert.IsTrue(File.ReadAllText(sally.UserFile.Path).Contains("sallyWasHere"));

					//and over at Bob's house, it's as if Sally had never connected

					bob.AssertHeadCount(1);
					Assert.AreEqual("bob", bob.Repository.GetTip().UserId);
					Assert.IsTrue(File.ReadAllText(bob.UserFile.Path).Contains("bobWasHere"));
				}
			}
		}


		[Test]
		public void Sync_MergeFailure_NoneOfTheOtherGuysFilesMakeItIntoWorkingDirectory()
		{
			using (var bob = new RepositorySetup("bob"))
			{
				bob.ProjectFolderConfig.IncludePatterns.Add("*.txt");
				bob.AddAndCheckinFile("aaa.txt", "apple");
				bob.AddAndCheckinFile("bbb.txt", "bread");
				bob.AddAndCheckinFile("zzz.txt", "zoo");
				using (var sally = new RepositorySetup("sally", bob))
				{
					bob.AddAndCheckinFile("aaa.txt", "bob-apple");
					bob.AddAndCheckinFile("bbb.txt", "bob-bread");
					bob.AddAndCheckinFile("zzz.txt", "bob-zoo");
				   using (new FailureSimulator("TextMerger-bbb.txt"))
					{
						sally.AddAndCheckinFile("aaa.txt", "sally-apple");
						sally.AddAndCheckinFile("bbb.txt", "sally-bread");
						sally.AddAndCheckinFile("zzz.txt", "sally-zipper");
						Assert.IsFalse(sally.CheckinAndPullAndMerge(bob).Succeeded);

					   //make sure we ended up on Sally's revision, even though Bob's are newer
						var currentRevision = sally.Repository.GetRevisionWorkingSetIsBasedOn();
						Assert.AreEqual("sally", sally.Repository.GetRevision(currentRevision.Number.Hash).UserId);

					   //sally should see no changes, because it should all be rolled back
						sally.AssertFileContents("aaa.txt", "sally-apple");
						sally.AssertFileContents("bbb.txt", "sally-bread");
						sally.AssertFileContents("zzz.txt", "sally-zipper");

//                        sally.ShowInTortoise();
					   sally.AssertHeadCount(2);
						Assert.IsFalse(sally.GetProgressString().Contains("creates new remote heads"));
					}
				}
			}
		}


		/// <summary>
		/// regression test: there was a bug (found before we released) where on rollback
		/// we were going to the tip, which if this was the *second* attempt, could be the other guy's work!
		/// </summary>
		[Test]
		public void Sync_RepeatedMergeFailure_WeAreLeftOnOurOwnWorkingDefault()
		{
			using (var bob = new RepositoryWithFilesSetup("bob", "test.txt", "hello"))
			using (var sally = RepositoryWithFilesSetup.CreateByCloning("sally",bob))
			using (new FailureSimulator("TextMerger-test.txt"))
			{
				bob.WriteNewContentsToTestFile("bobWasHere");
				bob.AddAndCheckIn();
				sally.WriteNewContentsToTestFile("sallyWasHere");
				var result = sally.CheckinAndPullAndMerge(bob);
				Assert.IsFalse(result.Succeeded);

				//make sure we ended up on Sally's revision, even though Bob's are newer
				var currentRevision = sally.Repository.GetRevisionWorkingSetIsBasedOn();
				Assert.AreEqual("sally", sally.Repository.GetRevision(currentRevision.Number.Hash).UserId);

				//Now do it again

				bob.WriteNewContentsToTestFile("bobWasHere2");
				bob.AddAndCheckIn();
				Assert.AreEqual("bob", sally.Repository.GetTip().UserId,"if bob's not the tip, we're not testing the right situation");

				result = sally.CheckinAndPullAndMerge(bob);
				Assert.IsFalse(result.Succeeded);
				result = sally.CheckinAndPullAndMerge(bob);

				Assert.AreEqual("sally",sally.Repository.GetRevisionWorkingSetIsBasedOn().UserId);


				//sally.ShowInTortoise();

			}
		}

		[Test]
		public void Sync_FileLockedForWritingDuringUpdate_GetUpdatedFileOnceLockIsGone()
		{
			HgRunner.TimeoutSecondsOverrideForUnitTests = 1;

		   using (var bob = new RepositorySetup("bob"))
			{
				bob.ProjectFolderConfig.IncludePatterns.Add("*.txt");
				bob.AddAndCheckinFile("one.txt", "hello");
				using (var sally = new RepositorySetup("sally", bob))
				{
					bob.AddAndCheckinFile("one.txt", "hello-bob");
					using (sally.GetFileLockForWriting("one.txt"))
					{
						Assert.IsFalse(sally.CheckinAndPullAndMerge(bob).Succeeded);
					 sally.AssertFileContents("one.txt", "hello");
				   }
					sally.AssertSingleHead();

					//ok, now whatever was holding that file is done with it, and we try again

					Assert.IsTrue(sally.CheckinAndPullAndMerge(bob).Succeeded);
					sally.AssertFileContents("one.txt", "hello-bob");
				}
			}
		}

		[Test]
		public void Sync_FileLockedForReadingDuringMerge_LeftWithMultipleHeads()
		{
			HgRunner.TimeoutSecondsOverrideForUnitTests = 1;
			using (var bob = new RepositorySetup("bob"))
			{
				bob.ProjectFolderConfig.IncludePatterns.Add("*.txt");
				bob.AddAndCheckinFile("one.txt", "hello");
				using (var sally = new RepositorySetup("sally", bob))
				{
					bob.AddAndCheckinFile("one.txt", "hello-bob");
					using (sally.GetFileLockForReading("one.txt"))
					{
						sally.CheckinAndPullAndMerge(bob);
					}
					sally.AssertHeadCount(2);
				}
			}
		}

		[Test]
		public void Sync_BothChangedBinaryFile_FailureReportedOneChosenSingleHead()
		{
			using (RepositoryWithFilesSetup bob = new RepositoryWithFilesSetup("bob", "test.a9a", "original"))
			{
				using (RepositoryWithFilesSetup sally = RepositoryWithFilesSetup.CreateByCloning("sally", bob))
				{
					bob.ReplaceSomething("bobWasHere");
					bob.AddAndCheckIn();
					sally.ReplaceSomething("sallyWasHere");

					//now we have a merge of a file type that don't know how to merge
					sally.CheckinAndPullAndMerge(bob);

					sally.AssertSingleHead();
					bob.AssertSingleHead();

					//sally.AssertSingleConflict(c => c.GetType == typeof (UnmergableFileTypeConflict));
					sally.AssertSingleConflictType<UnmergableFileTypeConflict>();

					//nb: this is bob becuase the conflict handling mode is (at the time of this test
					//writing) set to TheyWin.
					Assert.IsTrue(File.ReadAllText(sally.UserFile.Path).Contains("bobWasHere"));
				}

			}
		}


		[Test]
		public void Sync_TheyHaveAFileWhichWeAlsoEditedButHavenotCheckedIn_OursIsRenamedToSafetyAndWeGetTheirs()
		{
			using (RepositoryWithFilesSetup bob = new RepositoryWithFilesSetup("bob", "test.a9a", "original"))
			{
				using (RepositoryWithFilesSetup sally = RepositoryWithFilesSetup.CreateByCloning("sally", bob))
				{
					File.WriteAllText(bob.ProjectFolder.Combine("problem.txt"), "bobs problem");
					bob.ProjectConfiguration.IncludePatterns.Add("problem.txt");
					bob.AddAndCheckIn();
					sally.ReplaceSomething("sallyWasHere");
					File.WriteAllText(sally.ProjectFolder.Combine("problem.txt"), "sally's problem");
					//notice, we don't alter the include patter on sally, so this doesn't get checked in
					// on her side

					sally.CheckinAndPullAndMerge(bob);

					sally.AssertNoErrorsReported();

					var rescueFiles = Directory.GetFiles(sally.ProjectFolder.Path, "*.chorusRescue");
					Assert.AreEqual(1, rescueFiles.Length);
					Assert.AreEqual("sally's problem", File.ReadAllText(rescueFiles[0]));
					sally.AssertFileContents("problem.txt", "bobs problem");
				}

			}
		}

		/// <summary>
		/// regression test, for situation where RemoveMergeObstacles was over-zealou
		/// </summary>
		[Test]
		public void Sync_WeHaveUntrackedFile_NotRenamed()
		{
			using (RepositoryWithFilesSetup bob = new RepositoryWithFilesSetup("bob", "test.a9a", "original"))
			{
				using (RepositoryWithFilesSetup sally = RepositoryWithFilesSetup.CreateByCloning("sally", bob))
				{
					File.WriteAllText(bob.ProjectFolder.Combine("somethingNew.txt"), "blah");
					bob.ProjectConfiguration.IncludePatterns.Add("somethingNew.txt");
					bob.AddAndCheckIn();
					sally.ReplaceSomething("sallyWasHere");
					File.WriteAllText(sally.ProjectFolder.Combine("untracked.txt"), "foo");
					sally.CheckinAndPullAndMerge(bob);

					sally.AssertNoErrorsReported();

					var rescueFiles = Directory.GetFiles(sally.ProjectFolder.Path, "*.chorusRescue");
					Assert.AreEqual(0, rescueFiles.Length);
				}
			}
		}

		/// <summary>
		/// regression test (WS-14964), where the user had actually acquired 6 heads that needed to be merged.
		/// </summary>
		[Test]
		public void Sync_MergeWhenThereIsMoreThanOneHeadToMergeWith_MergesBoth()
		{
			using (RepositoryWithFilesSetup bob = new RepositoryWithFilesSetup("bob", "test.a9a", "original"))
			using (RepositoryWithFilesSetup sally = RepositoryWithFilesSetup.CreateByCloning("sally", bob))
			{
				var tip = sally.Repository.GetTip();
				sally.ReplaceSomething("forbranch1");
				sally.AddAndCheckIn();
				 sally.Repository.Update(tip.Number.Hash);

				sally.Repository.Branch("branch1");
				sally.ReplaceSomething("forbranch1");
				sally.AddAndCheckIn();
				 sally.Repository.Update(tip.Number.Hash);

			   sally.Repository.Branch("branch2");
				sally.ReplaceSomething("forbranch2");
				sally.AddAndCheckIn();
				sally.Repository.Update(tip.Number.Hash);

				sally.Repository.Branch("branch3");
				sally.ReplaceSomething("forbranch3");
				sally.AddAndCheckIn();
				sally.Repository.Update(tip.Number.Hash);

				sally.AssertHeadCount(4);

				bob.ReplaceSomething("bobWasHere");
				bob.AddAndCheckIn();
				sally.ReplaceSomething("sallyWasHere");
				sally.CheckinAndPullAndMerge(bob);

				sally.AssertNoErrorsReported();

				var rescueFiles = Directory.GetFiles(sally.ProjectFolder.Path, "*.chorusRescue");
				Assert.AreEqual(0, rescueFiles.Length);

				sally.AssertHeadCount(1);
			}

		}

		/// <summary>
		/// the diff here with the previous test is that while sally is still the one who is the driver
		/// (she dose the merge and push to bob), this time we follow up with bob doing a sync, which
		/// is essentially just a pull and update, to make sure that at that point the system renames
		/// his offending file (which Sally's chorus would have know way of knowing about, since it's
		/// not in his repository).
		/// </summary>
		[Test]
		public void Sync_WeHaveAFileWhichTheyAlsoEditedButHavenotCheckedIn_TheirsIsRenamedToSafetyAndTheyGetOurs()
		{
			using (RepositoryWithFilesSetup bob = new RepositoryWithFilesSetup("bob", "test.a9a", "original"))
			{
				using (RepositoryWithFilesSetup sally = RepositoryWithFilesSetup.CreateByCloning("sally", bob))
				{
					File.WriteAllText(bob.ProjectFolder.Combine("problem.txt"), "bob's problem");
					//notice, we don't alter the include pattern on bob, so this doesn't get checked in
					// on his side
					bob.AddAndCheckIn();

					sally.ReplaceSomething("sallyWasHere");
					File.WriteAllText(sally.ProjectFolder.Combine("problem.txt"), "sally's problem");
					sally.ProjectConfiguration.IncludePatterns.Add("problem.txt");

					sally.CheckinAndPullAndMerge(bob);
					sally.AssertNoErrorsReported();

					//ok, so the problem is now lurking in bob's repo, but it doesn't hit him until
					//he does at least an update

					bob.CheckinAndPullAndMerge(sally);

					var rescueFiles = Directory.GetFiles(bob.ProjectFolder.Path, "*.chorusRescue");
					Assert.AreEqual(1, rescueFiles.Length);
					Assert.AreEqual("bob's problem", File.ReadAllText(rescueFiles[0]));
					sally.AssertFileContents("problem.txt", "sally's problem");
				}

			}
		}

//        [Test, Ignore("by hand only")]
//        public void TryingToReproduceNullsAtEndOfFile()
//        {
//            using (MemoryStream memoryStream = new MemoryStream())
//            {
//                memoryStream.Write(new byte[]{60},0,1 );
//                string xmlString = Encoding.UTF8.GetString(memoryStream.ToArray());
//                Assert.IsFalse(xmlString.Contains("\0"));
//
//                xmlString = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Position);
//                Assert.IsFalse(xmlString.Contains("\0"));
//
//                xmlString = Encoding.UTF8.GetString(memoryStream.GetBuffer());
//                Assert.IsFalse(xmlString.Contains("\0"));
//            }
//        }
	}
}