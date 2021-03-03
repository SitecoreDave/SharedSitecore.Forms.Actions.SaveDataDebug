using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.ExperienceForms.Data;
using Sitecore.ExperienceForms.Data.Entities;
using Sitecore.ExperienceForms.Models;
using Sitecore.ExperienceForms.Processing;
using Sitecore.ExperienceForms.Processing.Actions;

namespace SharedSitecore.Forms.Actions
{
    /// <summary>
    /// Executes a submit action saving the form data in ExperienceForms db enabling breakpoint/debug
    /// </summary>
    /// <seealso cref="Sitecore.ExperienceForms.Processing.Actions.SubmitActionBase{TParametersData}" />
    public class SaveDataDebug : SubmitActionBase<string>
    {
      	private IFormDataProvider _dataProvider;

        private IFileStorageProvider _fileStorageProvider;

        protected virtual IFormDataProvider FormDataProvider => _dataProvider ?? (_dataProvider = ServiceLocator.ServiceProvider.GetService<IFormDataProvider>());

        protected virtual IFileStorageProvider FileStorageProvider => _fileStorageProvider ?? (_fileStorageProvider = ServiceLocator.ServiceProvider.GetService<IFileStorageProvider>());

        public SaveDataDebug(ISubmitActionData submitActionData)
            : base(submitActionData)
        {
        }

        public SaveDataDebug(ISubmitActionData submitActionData, IFormDataProvider dataProvider, IFileStorageProvider fileStorageProvider)
            : this(submitActionData)
        {
            Assert.ArgumentNotNull(dataProvider, "dataProvider");
            Assert.ArgumentNotNull(dataProvider, "fileStorageProvider");
            _dataProvider = dataProvider;
            _fileStorageProvider = fileStorageProvider;
        }

        protected override bool TryParse(string value, out string target)
        {
            target = string.Empty;
            return true;
        }

        protected override bool Execute(string data, FormSubmitContext formSubmitContext)
        {
            Assert.ArgumentNotNull(formSubmitContext, "formSubmitContext");
            return SavePostedData(formSubmitContext.FormId, formSubmitContext.SessionId, formSubmitContext.Fields);
        }

        protected virtual bool SavePostedData(Guid formId, Guid sessionId, IList<IViewModel> postedFields)
        {
            try
            {
                List<Guid> list = new List<Guid>();
                FormEntry formEntry = new FormEntry
                {
                    Created = DateTime.UtcNow,
                    FormItemId = formId,
                    FormEntryId = sessionId,
                    Fields = new List<FieldData>()
                };
                if (postedFields != null)
                {
                    foreach (IViewModel postedField in postedFields)
                    {
                        AddFieldData(postedField, formEntry);
                        list.AddRange(GetCommittedFileIds(postedField));
                    }
                }
                FormDataProvider.CreateEntry(formEntry);
                if (list.Any())
                {
                    FileStorageProvider.CommitFiles(list);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message, ex, this);
                return false;
            }
        }

        protected static void AddFieldData(IViewModel postedField, FormEntry formEntry)
        {
            Assert.ArgumentNotNull(postedField, "postedField");
            Assert.ArgumentNotNull(formEntry, "formEntry");
            if ((postedField as IValueField)?.AllowSave ?? false)
            {
                object obj = postedField.GetType().GetProperty("Value")?.GetValue(postedField);
                if (obj != null)
                {
                    string valueType = obj.GetType().ToString();
                    string value = ParseFieldValue(obj);
                    FieldData item = new FieldData
                    {
                        FieldDataId = Guid.NewGuid(),
                        FieldItemId = Guid.Parse(postedField.ItemId),
                        FieldName = postedField.Name,
                        FormEntryId = formEntry.FormEntryId,
                        Value = value,
                        ValueType = valueType
                    };
                    formEntry.Fields.Add(item);
                }
            }
        }

        protected static string ParseFieldValue(object postedValue)
        {
            Assert.ArgumentNotNull(postedValue, "postedValue");
            List<string> list = new List<string>();
            IList list2 = postedValue as IList;
            if (list2 != null)
            {
                foreach (object item in list2)
                {
                    list.Add(item.ToString());
                }
            }
            else
            {
                list.Add(postedValue.ToString());
            }
            return string.Join(",", list);
        }

        protected static IList<Guid> GetCommittedFileIds(IViewModel postedField)
        {
            Assert.ArgumentNotNull(postedField, "postedField");
            List<Guid> committedFileIds = new List<Guid>();
            if (!((postedField as IValueField)?.AllowSave ?? false))
            {
                return committedFileIds;
            }
            object obj = postedField.GetType().GetProperty("Value")?.GetValue(postedField);
            if (obj == null)
            {
                return committedFileIds;
            }
            (obj as List<StoredFileInfo>)?.ForEach(delegate(StoredFileInfo fileInfo)
            {
                committedFileIds.Add(fileInfo.FileId);
            });
            return committedFileIds;
        }
    }
}