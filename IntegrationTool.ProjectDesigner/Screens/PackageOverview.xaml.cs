﻿using IntegrationTool.DiagramDesigner;
using IntegrationTool.ApplicationCore;
using IntegrationTool.ApplicationCore.Serialization;
using IntegrationTool.Flowmanagement;
using IntegrationTool.ProjectDesigner.Classes;
using IntegrationTool.ProjectDesigner.MenuWindows;
using IntegrationTool.ProjectDesigner.Screens.UserControls;
using IntegrationTool.SDK;
using IntegrationTool.SDK.Database;
using IntegrationTool.SDK.Diagram;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using IntegrationTool.ProjectDesigner.FlowDesign;

namespace IntegrationTool.ProjectDesigner.Screens
{
    /// <summary>
    /// Interaction logic for PackageOverview.xaml
    /// </summary>
    public partial class PackageOverview : UserControl
    {
        public event EventHandler BackButtonClicked;
        public event EventHandler ProgressReport;

        public Package Package { get; set; }
        private ModuleLoader moduleLoader;
        public ObservableCollection<ConnectionConfigurationBase> Connections { get; set; }
        private DesignerItem doubleClickedMainflowDesignerItem { get; set; }

        private FlowDesign.FlowDesigner mainFlowDesigner;

        public PackageOverview(ModuleLoader moduleLoader, ObservableCollection<ConnectionConfigurationBase> connections, Package package)
        {
            InitializeComponent();
            this.moduleLoader = moduleLoader;
            this.Connections = connections;
            this.DataContext = this.Package = package;

            InitializePackageOverview();
        }

        public void InitializePackageOverview()
        {
            // Initialize Mainflow Designer            
            mainFlowDesigner = new FlowDesign.FlowDesigner(this.Package, moduleLoader.Modules, DesignerCanvasType.MainFlow);
            mainFlowDesigner.MyDesigner.LoadDiagramFromXElement(this.Package.Diagram.Diagram);
            mainFlowDesigner.DesignerItemClick += mainFlowDesigner_DesignerItemClick;
            mainFlowDesigner.DesignerItemDoubleClick += mainFlowDesigner_DesignerItemDoubleClick;
            mainFlowDesigner.DesignerClicked += designerClicked;
            mainFlowDesigner.MyDesigner.OnDeleteCurrentSelection += MyDesigner_OnDeleteCurrentSelection;
            this.mainFlowContent.Content = mainFlowDesigner;
            InitializeSubFlowDesigner();            
        }

        void MyDesigner_OnDeleteCurrentSelection(object sender, EventArgs e)
        {
            DesignerCanvas designerCanvas = sender as DesignerCanvas;
            foreach(DesignerItem designerItem in designerCanvas.SelectionService.CurrentSelection.OfType<DesignerItem>())
            {
                this.Package.Configurations.RemoveAll(t => t.ConfigurationId == designerItem.ID);
            }
        }


        void designerClicked(object sender, EventArgs e)
        {
            propertiesRow.Height = new GridLength(0, GridUnitType.Pixel);
            PropertiesContentControl.Content = null;
            propertiesSplitter.Visibility = System.Windows.Visibility.Hidden;
        }

        void mainFlowDesigner_DesignerItemClick(object sender, EventArgs e)
        {
            StepProperty stepProperty = new StepProperty();
            stepProperty.DesignerItem = ((RoutedEventArgs)e).OriginalSource as DesignerItem;
            stepProperty.Configuration = GetStepConfiguration(stepProperty.DesignerItem.ID, stepProperty.DesignerItem.ModuleDescription, this.Package);

            propertiesRow.Height = new GridLength(100, GridUnitType.Star);
            this.PropertiesContentControl.Content = new PropertiesControl(stepProperty);
            this.propertiesSplitter.Visibility = System.Windows.Visibility.Visible;
        }

        private void InitializeSubFlowDesigner()
        {
            // Initialize Subflow Designer
            var subFlowDesigner = new FlowDesign.FlowDesigner(this.Package, moduleLoader.Modules, DesignerCanvasType.SubFlow);
            subFlowDesigner.DesignerItemDoubleClick += subFlowDesigner_DesignerItemDoubleClick;
            subFlowDesigner.SubflowMagnifyIconDoubleClick += subFlowDesigner_SubflowMagnifyIconDoubleClick;
            propertiesRow.Height = new GridLength(0);
            this.subFlowContent.Content = subFlowDesigner;
        }

        void subFlowDesigner_SubflowMagnifyIconDoubleClick(object sender, EventArgs e)
        {
            DesignerItem designerItem = ((RoutedEventArgs)e).OriginalSource as DesignerItem;
            SubFlowExecution subFlowExecution = GetSubflowExecution();
            IDatastore dataStore = subFlowExecution.GetDataObjectForDesignerItem(designerItem.ID, null);

            DataPreviewWindow dataPreviewWindow = new DataPreviewWindow(dataStore);
            dataPreviewWindow.Show();
        }

        private StepConfigurationBase GetStepConfiguration(Guid designerItemId, ModuleDescription itemModuleDescription, Package package)
        {
            StepConfigurationBase configuration = package.Configurations.Where(t => t.ConfigurationId == designerItemId).FirstOrDefault() as StepConfigurationBase;
            if (configuration == null)
            {
                configuration = Activator.CreateInstance(itemModuleDescription.Attributes.ConfigurationType) as StepConfigurationBase;
                configuration.ConfigurationId = designerItemId;
                package.Configurations.Add(configuration);
            }

            return configuration;
        }

        void subFlowDesigner_DesignerItemDoubleClick(object sender, EventArgs e)
        {
            DesignerItem designerItem = sender as DesignerItem;

            StepConfigurationBase configuration = GetStepConfiguration(designerItem.ID, designerItem.ModuleDescription, this.Package);

            SaveSubflowConfiguration();

            SubFlowExecution subFlowExecution = GetSubflowExecution();
            IDatastore dataStore = subFlowExecution.GetDataObjectForDesignerItem(designerItem.ID, null);

            ConfigurationWindowSettings configurationWindowSettings = ConfigurationWindowSettings.Get(designerItem, configuration, this.moduleLoader, dataStore, Connections);

            ShowConfiguationWindow(configurationWindowSettings);
        }

        void ShowConfiguationWindow(ConfigurationWindowSettings configurationWindowSettings)
        {
            ConfigurationWindow configurationWindow = new ConfigurationWindow(configurationWindowSettings);
            configurationWindow.Closing += configurationWindow_Closing;
            configurationWindow.ShowDialog();
        }        

        void configurationWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ConfigurationWindow configurationWindow = sender as ConfigurationWindow;
            ConfigurationWindowSettings settings = configurationWindow.ConfigurationWindowSettings;
            if(configurationWindow.Status == Classes.WindowResult.Canceled)
            {
                Type configurationType = settings.configuration.GetType();
                object revertedConfiguration = ConfigurationSerializer.DeserializeObject(
                                                    settings.originalConfiguration,
                                                    configurationType,
                                                    moduleLoader.GetModuleTypeList()
                                                    );

                int indexConfig = Package.Configurations.IndexOf(settings.configuration);
                Package.Configurations[indexConfig] = revertedConfiguration as StepConfigurationBase;
            }            
        }

        public SubFlowExecution GetSubflowExecution()
        {
            ObjectResolver objectResolver = new ObjectResolver(Package.Configurations.OfType<StepConfigurationBase>().ToList(), Connections);
            SerializedDiagram subDiagram = this.Package.SubDiagrams.Where(t => t.ParentItemId == doubleClickedMainflowDesignerItem.ID).FirstOrDefault();
            IntegrationTool.SDK.Diagram.DiagramDeserializer deserializer = new SDK.Diagram.DiagramDeserializer(moduleLoader.Modules, subDiagram.Diagram);
            SubFlowExecution subFlowExecution = new SubFlowExecution(null, null, objectResolver, deserializer.DesignerItems, deserializer.Connections);

            return subFlowExecution;
        }

        void mainFlowDesigner_DesignerItemDoubleClick(object sender, EventArgs e)
        {
            doubleClickedMainflowDesignerItem = sender as DesignerItem;
            if (doubleClickedMainflowDesignerItem.ModuleDescription.Attributes.ContainsSubConfiguration)
            {
                packageDesignerTabControl.SelectedIndex = 1;
            }
            else
            {
                StepConfigurationBase configuration = GetStepConfiguration(doubleClickedMainflowDesignerItem.ID, doubleClickedMainflowDesignerItem.ModuleDescription, this.Package);
                ConfigurationWindowSettings configurationWindowSettings = ConfigurationWindowSettings.Get(doubleClickedMainflowDesignerItem, configuration, this.moduleLoader, null, Connections);
                ShowConfiguationWindow(configurationWindowSettings);
            }
        }

        public void SaveDiagram()
        {
            var mainFlowDesigner = this.mainFlowContent.Content as FlowDesign.FlowDesigner;
            if(mainFlowDesigner != null)
            {
                this.Package.Diagram.Diagram = mainFlowDesigner.MyDesigner.SaveDiagramToXElement();
            }

            SaveSubflowConfiguration();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            SaveDiagram();
            if(BackButtonClicked != null)
            {
                BackButtonClicked(sender, e);
            }
        }

        private void packageDesignerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch(packageDesignerTabControl.SelectedIndex)
            {
                case 0:
                    this.subFlowTab.Visibility = System.Windows.Visibility.Hidden;
                    subFlowContent.Visibility = Visibility.Hidden;
                    SaveSubflowConfiguration();
                    this.doubleClickedMainflowDesignerItem = null;
                    this.ToolboxContent.Content = new ToolboxMainflow(moduleLoader.Modules);
                    break;

                case 1:
                    InitializeSubFlowDesigner();
                    LoadSubflowConfiguration();
                    this.subFlowTab.Visibility = System.Windows.Visibility.Visible;
                    subFlowContent.Visibility = Visibility.Visible;
                    this.ToolboxContent.Content = new ToolboxSubflow(moduleLoader.Modules);
                    break;
            }
        }

        private void SaveSubflowConfiguration()
        {
            if(doubleClickedMainflowDesignerItem != null)
            {
                SerializedDiagram subDiagram = this.Package.SubDiagrams.Where(t => t.ParentItemId == doubleClickedMainflowDesignerItem.ID).FirstOrDefault();

                var subFlowDesigner = this.subFlowContent.Content as FlowDesign.FlowDesigner;
                if (subFlowDesigner != null && subDiagram != null)
                {
                    subDiagram.Diagram = subFlowDesigner.MyDesigner.SaveDiagramToXElement();
                }
            }
        }

        private void LoadSubflowConfiguration()
        {
            if (doubleClickedMainflowDesignerItem != null)
            {
                SerializedDiagram subDiagram = this.Package.SubDiagrams.Where(t => t.ParentItemId == doubleClickedMainflowDesignerItem.ID).FirstOrDefault();
                if (subDiagram == null)
                {
                    subDiagram = new SerializedDiagram();
                    subDiagram.ParentItemId = doubleClickedMainflowDesignerItem.ID;
                    this.Package.SubDiagrams.Add(subDiagram);
                }
                else
                {
                    var subFlowDesigner = this.subFlowContent.Content as FlowDesign.FlowDesigner;
                    if (subFlowDesigner != null)
                    {
                        subFlowDesigner.MyDesigner.LoadDiagramFromXElement(subDiagram.Diagram);
                    }
                }
            }
        }

        private void btnRunPackage_Click(object sender, RoutedEventArgs e)
        {
            if (mainFlowContent.Content == null)
            {
                return;
            }

            SaveDiagram();            

            FlowManager flowManager = new FlowManager(this.Connections, this.moduleLoader.Modules,  this.Package);

            FlowDesign.FlowDesigner mainFlowDesigner = mainFlowContent.Content as FlowDesign.FlowDesigner;
            mainFlowDesigner.MyDesigner.ExecuteFlow(flowManager);
            flowManager.DesignerItemStart += flowManager_ProgressReport;
            flowManager.ProgressReport += flowManager_ProgressReport;
            flowManager.DesignerItemStop += flowManager_ProgressReport;

            AdditionalInfosArea.Height = new GridLength(120);

            RunLog runLog = new RunLog();
            this.Package.ParentProject.RunLogs.Add(runLog);
            flowManager.Run(runLog);
        }

        void flowManager_ProgressReport(object sender, EventArgs e)
        {
            if(ProgressReport != null)
            {
                ProgressReport(sender, e);
            }
        }
        
    }
}
