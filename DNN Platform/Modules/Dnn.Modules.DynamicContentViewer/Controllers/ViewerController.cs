﻿// Copyright (c) DNN Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Web.Mvc;
using Dnn.DynamicContent;
using Dnn.Modules.DynamicContentViewer.Components;
using DotNetNuke.Collections;
using DotNetNuke.Common;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Actions;
using DotNetNuke.Security;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;

namespace Dnn.Modules.DynamicContentViewer.Controllers
{
    /// <summary>
    /// The ViewerController is used to manage the Actions associated with the Dynamic Content Viewer
    /// </summary>
    public class ViewerController : DnnController
    {
        #region Members

        private readonly IDynamicContentViewerManager _dynamicContentViewerManager;
        #endregion

        /// <summary>
        /// ViewerController Constructor
        /// </summary>
        public ViewerController()
        {
            _dynamicContentViewerManager = DynamicContentViewerManager.Instance;
        }
        /// <summary>
        /// The Edit Action will render the Content associated with this module using an Edit template
        /// </summary>
        /// <returns>The ViewResult</returns>
        [HttpGet]
        public ActionResult Edit()
        {
            string templateName;
            var templateId = _dynamicContentViewerManager.GetEditTemplateId(ActiveModule);
            if (templateId > -1)
            {
                var template = ContentTemplateManager.Instance.GetContentTemplate(templateId, PortalSettings.PortalId, true);
                templateName = GetTemplateName(template);
            }
            else
            {
                templateName = Globals.ApplicationPath + "/DesktopModules/MVC/Dnn/DynamicContentViewer/Views/Viewer/AutoGenerated.cshtml";
            }

            ViewData["Template"] = templateName;

            return View(_dynamicContentViewerManager.GetContentItem(ActiveModule));
        }

        /// <summary>
        /// The Edit Action will process the posted Form
        /// </summary>
        /// <param name="collection">The collection of Form name/value pairs that represents the Content Item's fields</param>
        /// <returns>The ViewResult</returns>
        [HttpPost]
        [ValidateInput(false)]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FormCollection collection)
        {
            var contentItem = _dynamicContentViewerManager.GetContentItem(ActiveModule);
            if (contentItem == null)
            {
                return RedirectToDefaultRoute();
            }

            ProcessFields(contentItem.Content, collection, String.Empty);
            _dynamicContentViewerManager.UpdateContentItem(contentItem);

            return RedirectToDefaultRoute();
        }


        /// <summary>
        /// GetIndexActions returns the DNN Module Actions associated with the Index MVC Action/View
        /// </summary>
        /// <returns>A collection of Module Actions</returns>
        public ModuleActionCollection GetIndexActions()
        {
            var actions = new ModuleActionCollection();

            var contentTypeId = _dynamicContentViewerManager.GetContentTypeId(ActiveModule);

            if (contentTypeId > -1)
            {
                actions.Add(-1,
                        LocalizeString("EditContent"),
                        ModuleActionType.AddContent,
                        "",
                        "",
                        ModuleContext.EditUrl("Edit"),
                        false,
                        SecurityAccessLevel.Edit,
                        true,
                        false);
            }

            var managerModule = ModuleController.Instance.GetModuleByDefinition(PortalSettings.PortalId, "Dnn.DynamicContentManager");

            if (managerModule != null && ModulePermissionController.HasModuleAccess(SecurityAccessLevel.Edit, "EDIT", managerModule))
            {
                actions.Add(-1,
                        LocalizeString("EditTemplates"),
                        ModuleActionType.AddContent,
                        "",
                        "",
                        Globals.NavigateURL(managerModule.TabID, String.Empty, "tab=Templates"),
                        false,
                        SecurityAccessLevel.Edit,
                        true,
                        false);
            }
            return actions;
        }

        private string GetTemplateName(ContentTemplate template)
        {
            string templateName = String.Empty;
            IFileInfo file = null;
            if (template != null)
            {
                file = FileManager.Instance.GetFile(template.TemplateFileId);
            }

            if (file != null)
            {
                if (file.PortalId > -1)
                {
                    templateName = PortalSettings.HomeDirectory + file.RelativePath;
                }
                else
                {
                    templateName = Globals.HostPath + file.RelativePath;
                }
            }
            return templateName;
        }

        /// <summary>
        /// The Index Action is the default Action for the controller - and will render the Content associated with this module
        /// </summary>
        /// <returns>The ViewResult</returns>
		[ModuleActionItems]
        public ActionResult Index()
        {
            string templateName;
            var contentTypeId = _dynamicContentViewerManager.GetContentTypeId(ActiveModule);
            var templateId = _dynamicContentViewerManager.GetViewTemplateId(ActiveModule);
            ContentTemplate template;
            if (templateId > -1)
            {
                template = ContentTemplateManager.Instance.GetContentTemplate(templateId, PortalSettings.PortalId, true);
                templateName = GetTemplateName(template);
            }
            else
            {
                if (contentTypeId > -1)
                {
                    templateName = Globals.ApplicationPath + "/DesktopModules/MVC/Dnn/DynamicContentViewer/Views/Viewer/AutoGeneratedView.cshtml";
                }
                else
                {
                    template = ContentTemplateManager.Instance.GetContentTemplates(PortalSettings.PortalId, true).SingleOrDefault(t => t.Name == "Getting Started");
                    templateName = GetTemplateName(template);
                }
            }

            if (String.IsNullOrEmpty(templateName))
            {
                return View("GettingStarted");
            }

            var contentItem = contentTypeId > -1 ? _dynamicContentViewerManager.GetOrCreateContentItem(ActiveModule, contentTypeId) 
                : _dynamicContentViewerManager.CreateDefaultContentItem(ActiveModule);
            
            ViewData["Template"] = templateName;

            return View(contentItem);
        }

        private void ProcessFields(DynamicContentPart contentPart, FormCollection collection, string prefix)
        {
            foreach (var field in contentPart.Fields.Values)
            {
                if (field.Definition.IsReferenceType)
                {
                    var part = field.Value as DynamicContentPart;
                    if (part != null)
                    {
                        var newPrefix = (String.IsNullOrEmpty(prefix)) ? field.Definition.Name : prefix + field.Definition.Name;
                        newPrefix += "/";
                        ProcessFields(part, collection, newPrefix);
                    }
                }
                else
                {
                    var fieldName = prefix + field.Definition.Name;

                    switch (field.Definition.DataType.Name)
                    {
                        case "Rich Text":
                            var html = collection[fieldName];
                            field.Value = String.IsNullOrEmpty(html) ? String.Empty : Server.HtmlEncode(html);
                            break;
                        case "Markdown":
                            var markdown = collection[fieldName];
                            field.Value = String.IsNullOrEmpty(markdown) ? String.Empty : markdown;
                            break;
                        default:
                            switch (field.Definition.DataType.UnderlyingDataType)
                            {
                                case UnderlyingDataType.Boolean:
                                    //Handle special case of Boolean values due to the way a checkbox works with MVC Helpers
                                    // return value is "true;false" if true and "false" if false
                                    field.Value = (collection[fieldName].Contains("true"));
                                    break;
                                case UnderlyingDataType.Integer:
                                    field.Value = collection.GetValueOrDefault(fieldName, 0);
                                    break;
                                case UnderlyingDataType.Float:
                                    field.Value = collection.GetValueOrDefault(fieldName, 0.0);
                                    break;
                                default:
                                    field.Value = collection.GetValueOrDefault(fieldName, String.Empty);
                                    break;
                            }
                            break;
                    }

                }
            }
        }
    }
}
