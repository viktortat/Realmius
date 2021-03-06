﻿////////////////////////////////////////////////////////////////////////////
//
// Copyright 2017 Rubius
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using Realmius.Contracts.Models;
using Realmius.Server;
using Realmius.Server.Configurations;
using Realmius.Server.Models;
using Realmius.Server.QuickStart;
using Realmius.Tests.Server.Models;

namespace Realmius.Tests.Server
{
    [TestFixture]
    public class UploadTests : TestBase
    {
        private Func<LocalDbContext> _contextFunc;
        private ShareEverythingConfiguration _config;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _contextFunc = () => new LocalDbContext();
            _config = new ShareEverythingConfiguration(_contextFunc, typeof(DbSyncObject), typeof(DbSyncObjectWithIgnoredFields), typeof(IdIntAutogeneratedObject), typeof(RefSyncObject));
        }

        [Test]
        public void UnknownType_Exception()
        {
            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(UnknownSyncObject)));
            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "UnknownSyncObject",
                         PrimaryKey = "123",
                         SerializedObject = JsonConvert.SerializeObject(new UnknownSyncObject()),
                     }
                }
            }, null);
            result.Results.Count.Should().Be(1);
            result.Results[0].Error.Should().ContainEquivalentOf("The entity type UnknownSyncObject is not part of the model for the current context");
        }

        public class IntFieldSentAsStringObject : IRealmiusObjectServer
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();

            public int IntField { get; set; }

            public string MobilePrimaryKey => Id;
        }
        [Test]
        public void IntFieldSentAsString()
        {
            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(IntFieldSentAsStringObject)));
            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = nameof(IntFieldSentAsStringObject),
                         PrimaryKey = "123",
                         SerializedObject = "{Id: \"123\", IntField: \"456\", MobilePrimaryKey: \"123\"}",
                     }
                }
            }, null);
            CheckNoError(result);


            _contextFunc().IntFieldSentAsStringObjects.Count().Should().Be(1);
            var q = _contextFunc().IntFieldSentAsStringObjects.First();
            q.IntField.Should().Be(456);


        }

        [Test]
        public void UnknownType_NotInDb_TypeDoesNotExist()
        {
            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(DbSyncObject)));
            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "UnknownSyncObject",
                         PrimaryKey = "123",
                         SerializedObject = JsonConvert.SerializeObject(new UnknownSyncObject()),
                     }
                }
            }, null);
            result.Results.Count.Should().Be(0);
        }


        [Test]
        public void ManyToManyRef()
        {
            var db = _contextFunc();

            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(RefSyncObject)));
            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                    new UploadRequestItem()
                    {
                        Type = nameof(RefSyncObject),
                        PrimaryKey = "123",
                        SerializedObject = "{Id: '123', Text: 'zxc'}",
                    },new UploadRequestItem()
                    {
                        Type = nameof(RefSyncObject),
                        PrimaryKey = "22",
                        SerializedObject = "{Id: '22', Text: 'qwe'}",
                    },
                    new UploadRequestItem()
                    {
                        Type = nameof(RefSyncObject),
                        PrimaryKey = "456",
                        SerializedObject = "{Id: '456', Text: 'qwe', References: ['123']}",
                    }
                }
            }, null);
            result.Results.Count.Should().Be(3);
            string.Join(", ", result.Results.Where(x => !x.IsSuccess).Select(x => x.Error)).Should().Be("");

            db = _contextFunc();
            var obj0 = db.RefSyncObjects.First(x => x.Id == "123");
            obj0.Text.Should().Be("zxc");
            var obj = db.RefSyncObjects.First(x => x.Id == "456");
            obj.Text.Should().Be("qwe");
            string.Join(", ", obj.References.Select(x => x.Text)).Should().BeEquivalentTo("zxc");

            var result2 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {

                    new UploadRequestItem()
                    {
                        Type = nameof(RefSyncObject),
                        PrimaryKey = "456",
                        SerializedObject = "{References:null}",
                    }
                }
            }, null);
            result2.Results.Count.Should().Be(1);
            string.Join(", ", result2.Results.Where(x => !x.IsSuccess).Select(x => x.Error)).Should().Be("");
            db = _contextFunc();
            string.Join(", ", db.RefSyncObjects.First(x => x.Id == "456").References.Select(x => x.Text)).Should().BeEquivalentTo("");


            var result3 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {

                    new UploadRequestItem()
                    {
                        Type = nameof(RefSyncObject),
                        PrimaryKey = "456",
                        SerializedObject = "{References:['22']}",
                    }
                }
            }, null);
            result3.Results.Count.Should().Be(1);
            string.Join(", ", result3.Results.Where(x => !x.IsSuccess).Select(x => x.Error)).Should().Be("");
            db = _contextFunc();
            string.Join(", ", db.RefSyncObjects.First(x => x.Id == "456").References.Select(x => x.Text)).Should().BeEquivalentTo("qwe");


            var result4 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {

                    new UploadRequestItem()
                    {
                        Type = nameof(RefSyncObject),
                        PrimaryKey = "456",
                        SerializedObject = "{References:['22', '123']}",
                    }
                }
            }, null);
            result4.Results.Count.Should().Be(1);
            string.Join(", ", result3.Results.Where(x => !x.IsSuccess).Select(x => x.Error)).Should().Be("");
            db = _contextFunc();
            string.Join(", ", db.RefSyncObjects.First(x => x.Id == "456").References.Select(x => x.Text).OrderBy(x => x)).Should().BeEquivalentTo("qwe, zxc");
        }


        [Test]
        public void KnownType_Saved1()
        {
            _contextFunc().DbSyncObjects.Count().Should().Be(0);

            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(DbSyncObject)));
            var objectToSave = new DbSyncObject()
            {
                Text = "123123123",
                Id = Guid.NewGuid().ToString()
            };
            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObject",
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = JsonConvert.SerializeObject(objectToSave),
                     }
                }
            }, null);
            result.Results.Count.Should().Be(1);
            CheckNoError(result);
            result.Results[0].MobilePrimaryKey.Should().Be(objectToSave.MobilePrimaryKey);

            _contextFunc().DbSyncObjects.Count().Should().Be(1);
        }

        [Test]
        public void KnownType_Updated()
        {
            _contextFunc().DbSyncObjects.Count().Should().Be(0);

            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(DbSyncObject)));
            var objectToSave = new DbSyncObject()
            {
                Text = "123123123",
                Id = Guid.NewGuid().ToString()
            };

            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObject",
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = JsonConvert.SerializeObject(objectToSave),
                     }
                }
            }, null);
            CheckNoError(result);
            _contextFunc().DbSyncObjects.Find(objectToSave.Id).Text.Should().BeEquivalentTo("123123123");

            objectToSave.Text = "zxc";
            result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObject",
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = JsonConvert.SerializeObject(objectToSave),
                     }
                }
            }, null);
            CheckNoError(result);
            _contextFunc().DbSyncObjects.Find(objectToSave.Id).Text.Should().BeEquivalentTo("zxc");
            _contextFunc().DbSyncObjects.Count().Should().Be(1);
        }


        [Test]
        public void Deleted()
        {
            _contextFunc().DbSyncObjects.Count().Should().Be(0);

            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(DbSyncObject)));
            var objectToSave = new DbSyncObject()
            {
                Text = "123123123",
                Id = Guid.NewGuid().ToString()
            };
            var db = _contextFunc();
            db.DbSyncObjects.Add(objectToSave);
            db.SaveChanges();

            Func<DownloadDataResponse> getDownloaded = () => controller.Download(new DownloadDataRequest()
            {
                Types = new[] { nameof(DbSyncObject) },
            }, new { });
            var downloaded1 = getDownloaded();
            downloaded1.ChangedObjects.Count.Should().Be(1);

            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = nameof(DbSyncObject),
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = "",
                         IsDeleted = true,
                     }
                }
            }, null);
            CheckNoError(result);
            _contextFunc().DbSyncObjects.Find(objectToSave.Id).Should().BeNull();

            var downloaded2 = getDownloaded();
            downloaded2.ChangedObjects.Count.Should().Be(0); //deleted items should not be downloaded during initial DB download
        }

        [Test]
        public void KnownType_PartialUpdate()
        {
            _contextFunc().DbSyncObjects.Count().Should().Be(0);

            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(DbSyncObject)));
            var objectToSave = new DbSyncObject()
            {
                Text = "123123123",
                Id = Guid.NewGuid().ToString()
            };

            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObject",
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = JsonConvert.SerializeObject(objectToSave),
                     }
                }
            }, null);
            CheckNoError(result);

            result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObject",
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = "{Text: 'asd'}",
                     }
                }
            }, null);
            CheckNoError(result);
            _contextFunc().DbSyncObjects.Find(objectToSave.Id).Text.Should().BeEquivalentTo("asd");
            _contextFunc().DbSyncObjects.Count().Should().Be(1);
        }

        public class SyncConfigForCustomId : ShareEverythingConfiguration
        {
            public SyncConfigForCustomId(Func<ChangeTrackingDbContext> contextFactoryFunc, Type typeToSync, params Type[] typesToSync) : base(contextFactoryFunc, typeToSync, typesToSync)
            {
            }

            public override object[] KeyForType(Type type, string itemPrimaryKey)
            {
                if (type == typeof(IdIntObject))
                {
                    return new object[] { int.Parse(itemPrimaryKey) };
                }
                if (type == typeof(IdGuidObject))
                {
                    return new object[] { Guid.Parse(itemPrimaryKey) };
                }
                return base.KeyForType(type, itemPrimaryKey);
            }
        }

        [Test]
        public void IdInteger()
        {
            _contextFunc().IdIntObjects.Count().Should().Be(0);

            var controller = new RealmiusServerProcessor(new SyncConfigForCustomId(_contextFunc, typeof(IdIntObject)));
            var objectToSave = new IdIntObject()
            {
                Text = "123123123",
                Id = 1,
            };

            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = nameof(IdIntObject),
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = JsonConvert.SerializeObject(objectToSave),
                     }
                }
            }, null);
            CheckNoError(result);

            result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = nameof(IdIntObject),
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = "{Text: 'asd'}",
                     }
                }
            }, null);
            CheckNoError(result);
            _contextFunc().IdIntObjects.Find(objectToSave.Id).Text.Should().BeEquivalentTo("asd");
            _contextFunc().IdIntObjects.Count().Should().Be(1);
        }

        [Test]
        public void IdAutoIncrementInteger()
        {
            var db = _contextFunc();
            db.IdIntObjects.Count().Should().Be(0);

            var controller = new RealmiusServerProcessor(new SyncConfigForCustomId(_contextFunc, typeof(IdIntAutogeneratedObject)));
            var objectToSave = new IdIntAutogeneratedObject()
            {
                Text = "123123123",
            };
            db.IdIntAutogeneratedObjects.Add(objectToSave);
            db.SaveChanges();

            var download = controller.Download(
                new DownloadDataRequest()
                {
                    Types = new List<string>() { nameof(IdIntAutogeneratedObject) },
                    LastChangeTime = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(1)).ToDictionary(),
                }, new { });

            download.ChangedObjects.Count.Should().Be(1);
            var changedObject = download.ChangedObjects.First();
            changedObject.MobilePrimaryKey.Should().BeEquivalentTo(objectToSave.Id.ToString());
            changedObject.MobilePrimaryKey.Should().NotBeEmpty();
        }


        [Test]
        public void IdGuid()
        {
            _contextFunc().IdGuidObjects.Count().Should().Be(0);

            var controller = new RealmiusServerProcessor(new SyncConfigForCustomId(_contextFunc, typeof(IdGuidObject)));
            var objectToSave = new IdGuidObject()
            {
                Text = "123123123",
                Id = Guid.NewGuid(),
            };

            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = nameof(IdGuidObject),
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = JsonConvert.SerializeObject(objectToSave),
                     }
                }
            }, null);
            CheckNoError(result);

            result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = nameof(IdGuidObject),
                         PrimaryKey = objectToSave.MobilePrimaryKey,
                         SerializedObject = "{Text: 'asd'}",
                     }
                }
            }, null);
            CheckNoError(result);
            _contextFunc().IdGuidObjects.Find(objectToSave.Id).Text.Should().BeEquivalentTo("asd");
            _contextFunc().IdGuidObjects.Count().Should().Be(1);
        }

        private void CheckNoError(UploadDataResponse result)
        {
            result.Results.Count.Should().BeGreaterThan(0);
            string.Join(", ", result.Results.Select(x => x.Error).Where(x => !string.IsNullOrEmpty(x)))
                .ShouldBeEquivalentTo("");
        }



        [Test]
        public void DoNotUpdateIgnoredFields()
        {
            _contextFunc().DbSyncObjectWithIgnoredFields.Count().Should().Be(0);

            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(DbSyncObjectWithIgnoredFields)));

            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObjectWithIgnoredFields",
                         PrimaryKey = "1",
                         SerializedObject = "{Id: '1', Text: '123', Tags: 'zxc'}",
                     }
                }
            }, null);
            CheckNoError(result);

            var db = _contextFunc();
            var obj = db.DbSyncObjectWithIgnoredFields.First();
            obj.Text.Should().Be("123");
            obj.Tags.Should().BeNullOrEmpty();



            var result2 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObjectWithIgnoredFields",
                         PrimaryKey = "1",
                         SerializedObject = "{Id: '1', Text: '1234', Tags: 'zxc'}",
                     }
                }
            }, null);
            CheckNoError(result2);

            var db2 = _contextFunc();
            var obj2 = db2.DbSyncObjectWithIgnoredFields.First();
            obj2.Text.Should().Be("1234");
            obj2.Tags.Should().BeNullOrEmpty();
        }




        [Test]
        public void UploadData_CheckAndProcessCanRetrieveUnmodifiedEntityFromDatabase()
        {
            _contextFunc().DbSyncObjectWithIgnoredFields.Count().Should().Be(0);

            var config = new Mock<ShareEverythingConfiguration>(_contextFunc, typeof(DbSyncObjectWithIgnoredFields))
            {
                CallBase = true
            };

            var controller = new RealmiusServerProcessor(config.Object);

            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObjectWithIgnoredFields",
                         PrimaryKey = "1",
                         SerializedObject = "{Id: '1', Text: '123', Tags: 'zxc'}",
                     }
                }
            }, null);
            CheckNoError(result);

            config.Setup(
                    x =>
                        x.CheckAndProcess(It.IsAny<CheckAndProcessArgs<object>>()))
                .Returns((CheckAndProcessArgs<object> args) =>
                {
                    var localDb = (LocalDbContext)args.Database;

                    var dbObj = (DbSyncObjectWithIgnoredFields)args.OriginalDbEntity;
                    dbObj.Text.Should().Be("123");
                    ((DbSyncObjectWithIgnoredFields)args.Entity).Text.Should().Be("asd");
                    return true;
                });

            var result2 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObjectWithIgnoredFields",
                         PrimaryKey = "1",
                         SerializedObject = "{Id: '1', Text: 'asd', Tags: 'zxc'}",
                     }
                }
            }, null);
            CheckNoError(result2);
            config.VerifyAll();


            var db2 = _contextFunc();
            var obj2 = db2.DbSyncObjectWithIgnoredFields.First();
            obj2.Text.Should().Be("asd");
            obj2.Tags.Should().BeNullOrEmpty();
        }


        [Test]
        public void UploadData_ModifyObjectWithinCheckAndProcess()
        {
            _contextFunc().DbSyncObjectWithIgnoredFields.Count().Should().Be(0);

            var config = new Mock<ShareEverythingConfiguration>(_contextFunc, typeof(DbSyncObjectWithIgnoredFields))
            {
                CallBase = true
            };

            var controller = new RealmiusServerProcessor(config.Object);

            var result = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObjectWithIgnoredFields",
                         PrimaryKey = "1",
                         SerializedObject = "{Id: '1', Text: '123', Tags: 'zxc'}",
                     }
                }
            }, null);
            CheckNoError(result);

            config.Setup(
                    x =>
                        x.CheckAndProcess(It.IsAny<CheckAndProcessArgs<object>>()))
                .Returns((CheckAndProcessArgs<object> args) =>
                {
                    ((DbSyncObjectWithIgnoredFields)args.Entity).Text = "qwe";
                    return true;
                });

            var result2 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObjectWithIgnoredFields",
                         PrimaryKey = "1",
                         SerializedObject = "{Id: '1', Text: 'asd', Tags: 'zxc'}",
                     }
                }
            }, null);
            CheckNoError(result2);

            var db2 = _contextFunc();
            var obj2 = db2.DbSyncObjectWithIgnoredFields.First();
            obj2.Text.Should().Be("qwe");
        }


        [Test]
        public void CloneWithOriginalValues_Test()
        {
            var db = _contextFunc();
            var obj = new IdIntObject()
            {
                Text = "asd",
                Tags = "qwe"
            };
            db.IdIntObjects.Add(obj);
            db.SaveChanges();

            var db2 = _contextFunc();
            var obj2 = db2.IdIntObjects.Find(obj.Id);
            obj2.Text = "xcv";
            var obj3 = db2.CloneWithOriginalValues(obj2);
            obj3.Text.Should().Be("asd");
        }

        [Test]
        public void CloneWithOriginalValues_NewObject_Test()
        {
            var db = _contextFunc();
            var obj = new IdIntObject()
            {
                Text = "asd",
                Tags = "qwe"
            };

            var obj3 = db.CloneWithOriginalValues(obj);
            obj3.Should().BeNull();
        }


        [Test]
        public void Upload_Delete_UploadAgain()
        {
            var controller = new RealmiusServerProcessor(new ShareEverythingConfiguration(_contextFunc, typeof(DbSyncObject)));
            Func<DownloadDataResponse> getDownloaded = () => controller.Download(new DownloadDataRequest()
            {
                Types = new[] { nameof(DbSyncObject) },
            }, new { });
            Func<string> dataToCompare = () =>
            {
                var data = getDownloaded();
                return string.Join(", ", data.ChangedObjects.Select(x => $"{x.IsDeleted}"));
            };


            var objectToSave = new DbSyncObject()
            {
                Text = "123123123",
                Id = Guid.NewGuid().ToString()
            };
            var result1 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                    new UploadRequestItem()
                    {
                        Type = nameof(DbSyncObject),
                        PrimaryKey = objectToSave.MobilePrimaryKey,
                        SerializedObject = JsonConvert.SerializeObject(objectToSave),
                        //IsDeleted = true,
                    }
                }
            }, null);
            dataToCompare().Should().BeEquivalentTo("False");

            var result2 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                    new UploadRequestItem()
                    {
                        Type = nameof(DbSyncObject),
                        PrimaryKey = objectToSave.MobilePrimaryKey,
                        SerializedObject = "",
                        IsDeleted = true,
                    }
                }
            }, null);
            //dataToCompare().Should().BeEquivalentTo("True");

            objectToSave.Text = "zxc";
            var result3 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                    new UploadRequestItem()
                    {
                        Type = nameof(DbSyncObject),
                        PrimaryKey = objectToSave.MobilePrimaryKey,
                        SerializedObject = JsonConvert.SerializeObject(objectToSave),
                        IsDeleted = false,
                    }
                }
            }, null);
            dataToCompare().Should().BeEquivalentTo("False");
        }

        [Test]
        public void UploadData_ModifyObjectWithinCheckAndProcess_RequestObject()
        {
            _contextFunc().DbSyncObjectWithIgnoredFields.Count().Should().Be(0);

            var config = new Mock<ShareEverythingConfiguration>(_contextFunc, typeof(DbSyncObjectWithIgnoredFields))
            {
                CallBase = true
            };

            var controller = new RealmiusServerProcessor(config.Object);

            var result = controller.Upload(new UploadDataRequest
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObjectWithIgnoredFields",
                         PrimaryKey = "1",
                         SerializedObject = "{Id: '1', Text: '123', Tags: 'zxc'}",
                     }
                }
            }, null);
            CheckNoError(result);

            config.Setup(
                    x =>
                        x.CheckAndProcess(It.IsAny<CheckAndProcessArgs<object>>()))
                .Returns((CheckAndProcessArgs<object> args) =>
                {
                    ((LocalDbContext)args.Database).DbSyncObjectWithIgnoredFields.Find(args.Entity.MobilePrimaryKey).Should().NotBeNull();
                    ((DbSyncObjectWithIgnoredFields)args.Entity).Text = "qwe";
                    return true;
                });

            var result2 = controller.Upload(new UploadDataRequest()
            {
                ChangeNotifications =
                {
                     new UploadRequestItem()
                     {
                         Type = "DbSyncObjectWithIgnoredFields",
                         PrimaryKey = "1",
                         SerializedObject = "{Id: '1', Text: 'asd', Tags: 'zxc'}",
                     }
                }
            }, null);
            CheckNoError(result2);

            var db2 = _contextFunc();
            var obj2 = db2.DbSyncObjectWithIgnoredFields.First();
            obj2.Text.Should().Be("qwe");
        }


        [Test]
        public void EF_CheckAndProcessCanRetrieveUnmodifiedEntityFromDatabase()
        {
            var db = _contextFunc();
            var obj = new DbSyncObject()
            {
                Id = "123",
                Text = "asd"
            };
            db.DbSyncObjects.Add(obj);
            db.SaveChanges();

            var db2 = _contextFunc();
            var obj2 = db2.DbSyncObjects.Find("123");

            obj2.Text = "zxc";
            var obj2Entry = db2.Entry(obj2);
            var m01 = obj2Entry.Property("Text").IsModified;
            var m02 = obj2Entry.Property("Tags").IsModified;
            obj2Entry.State = EntityState.Detached;


            var obj3 = db2.DbSyncObjects.Find("123");
            obj3.Text.Should().Be("asd");

            try
            {
                obj2Entry.State = EntityState.Modified;
            }
            catch (InvalidOperationException)
            {
                db2.Entry(obj3).State = EntityState.Detached;
                obj2Entry.State = EntityState.Modified;
            }

            var entry = db2.ChangeTracker.Entries().First();
            var m1 = entry.Property("Text").IsModified;
            var m2 = entry.Property("Tags").IsModified;
            db2.SaveChanges();

            var db3 = _contextFunc();
            var obj4 = db3.DbSyncObjects.Find("123");
            obj4.Text.Should().Be("zxc");
        }
    }

}
