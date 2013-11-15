﻿using AutoMapper;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Models.Mapping;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web;
using ObjectExtensions = Umbraco.Core.ObjectExtensions;

namespace Umbraco.Tests.TestHelpers
{
    /// <summary>
    /// A base test class used for umbraco tests whcih sets up the logging, plugin manager any base resolvers, etc... and
    /// ensures everything is torn down properly.
    /// </summary>
    [TestFixture]
    public abstract class BaseUmbracoApplicationTest : BaseUmbracoConfigurationTest
    {
        private static bool _mappersInitialized = false;
        private static readonly object Locker = new object();

        [SetUp]
        public override void Initialize()
        {
            base.Initialize();

            using (DisposableTimer.TraceDuration<BaseUmbracoApplicationTest>("init"))
            {
                TestHelper.InitializeContentDirectories();
                TestHelper.EnsureUmbracoSettingsConfig();

                //Create the legacy prop-eds mapping
                LegacyPropertyEditorIdToAliasConverter.CreateMappingsForCoreEditors();

                SetupPluginManager();

                SetupApplicationContext();

                InitializeMappers();

                FreezeResolution();
            }
            
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();

            //reset settings
            SettingsForTests.Reset();
            UmbracoContext.Current = null;
            TestHelper.CleanContentDirectories();
            TestHelper.CleanUmbracoSettingsConfig();
            //reset the app context, this should reset most things that require resetting like ALL resolvers
            ObjectExtensions.DisposeIfDisposable(ApplicationContext.Current);
            ApplicationContext.Current = null;
            ResetPluginManager();
            LegacyPropertyEditorIdToAliasConverter.Reset();
        }
        
        private void InitializeMappers()
        {
            lock (Locker)
            {
                //only need to initialize one time
                if (_mappersInitialized == false)
                {
                    _mappersInitialized = true;
                    Mapper.Initialize(configuration =>
                    {
                        var mappers = PluginManager.Current.FindAndCreateInstances<IMapperConfiguration>();
                        foreach (var mapper in mappers)
                        {
                            mapper.ConfigureMappings(configuration, ApplicationContext);
                        }
                    });       
                }                
            }            
        }

        /// <summary>
        /// By default this returns false which means the plugin manager will not be reset so it doesn't need to re-scan 
        /// all of the assemblies. Inheritors can override this if plugin manager resetting is required, generally needs
        /// to be set to true if the  SetupPluginManager has been overridden.
        /// </summary>
        protected virtual bool PluginManagerResetRequired
        {
            get { return false; }
        }

        /// <summary>
        /// Inheritors can resset the plugin manager if they choose to on teardown
        /// </summary>
        protected virtual void ResetPluginManager()
        {
            if (PluginManagerResetRequired)
            {
                PluginManager.Current = null;    
            }
        }

        /// <summary>
        /// Inheritors can override this if they wish to create a custom application context
        /// </summary>
        protected virtual void SetupApplicationContext()
        {
            //disable cache
            var cacheHelper = CacheHelper.CreateDisabledCacheHelper();

            ApplicationContext.Current = new ApplicationContext(
                //assign the db context
                new DatabaseContext(new DefaultDatabaseFactory()),
                //assign the service context
                new ServiceContext(new PetaPocoUnitOfWorkProvider(), new FileUnitOfWorkProvider(), new PublishingStrategy(), cacheHelper),
                cacheHelper)
            {
                IsReady = true
            };
        }

        /// <summary>
        /// Inheritors can override this if they wish to setup the plugin manager differenty (i.e. specify certain assemblies to load)
        /// </summary>
        protected virtual void SetupPluginManager()
        {
            if (PluginManager.Current == null || PluginManagerResetRequired)
            {
                PluginManager.Current = new PluginManager(false);    
            }
        }

        /// <summary>
        /// Inheritors can override this to setup any resolvers before resolution is frozen
        /// </summary>
        protected virtual void FreezeResolution()
        {
            Resolution.Freeze();
        }

        protected ApplicationContext ApplicationContext
        {
            get { return ApplicationContext.Current; }
        }
    }
}