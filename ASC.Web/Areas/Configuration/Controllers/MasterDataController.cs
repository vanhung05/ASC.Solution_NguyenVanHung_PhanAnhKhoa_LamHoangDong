using ASC.Business.Interfaces;
using ASC.Model.Models;
using ASC.Utilities;
using ASC.Web.Areas.Configuration.Models;
using ASC.Web.Controllers;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using System.ComponentModel;

namespace ASC.Web.Areas.Configuration.Controllers
{
    [Area("Configuration")]
    [Authorize(Roles = "Admin")]
    public class MasterDataController : BaseController
    {
        private readonly IMasterDataOperations _masterData;
        private readonly IMapper _mapper;

        public MasterDataController(IMasterDataOperations masterData, IMapper mapper)
        {
            _masterData = masterData;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> MasterKeys()
        {
            var masterKeys = await _masterData.GetAllMasterKeysAsync();
            var masterKeysViewModel = _mapper.Map<List<MasterDataKey>, List<MasterDataKeyViewModel>>(masterKeys);

            HttpContext.Session.SetSession("MasterKeys", masterKeysViewModel);

            return View(new MasterKeysViewModel
            {
                MasterKeys = masterKeysViewModel?.ToList() ?? new List<MasterDataKeyViewModel>(),
                MasterKeyInContext = new MasterDataKeyViewModel(),
                IsEdit = false
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MasterKeys(MasterKeysViewModel masterKeys)
        {
            masterKeys.MasterKeys = HttpContext.Session.GetSession<List<MasterDataKeyViewModel>>("MasterKeys")
                                    ?? new List<MasterDataKeyViewModel>();

            if (!masterKeys.IsEdit)
            {
                ModelState.Remove("MasterKeyInContext.RowKey");
                ModelState.Remove("MasterKeyInContext.PartitionKey");
            }

            if (!ModelState.IsValid)
            {
                masterKeys.MasterKeyInContext ??= new MasterDataKeyViewModel();
                return View(masterKeys);
            }

            var masterKey = _mapper.Map<MasterDataKey>(masterKeys.MasterKeyInContext);
            var currentUser = HttpContext.User.GetCurrentUserDetails();

            if (masterKeys.IsEdit)
            {
                masterKey.UpdatedBy = currentUser.Email ?? currentUser.Name;
                masterKey.UpdatedDate = DateTime.UtcNow;

                await _masterData.UpdateMasterKeyAsync(masterKeys.MasterKeyInContext.PartitionKey, masterKey);
            }
            else
            {
                masterKey.RowKey = Guid.NewGuid().ToString();
                masterKey.PartitionKey = masterKeys.MasterKeyInContext.Name;
                masterKey.IsDeleted = false;

                masterKey.CreatedBy = currentUser.Email ?? currentUser.Name;
                masterKey.CreatedDate = DateTime.UtcNow;

                masterKey.UpdatedBy = currentUser.Email ?? currentUser.Name;
                masterKey.UpdatedDate = DateTime.UtcNow;

                await _masterData.InsertMasterKeyAsync(masterKey);
            }

            return RedirectToAction(nameof(MasterKeys));
        }

        [HttpGet]
        public async Task<IActionResult> MasterValues()
        {
            ViewBag.MasterKeys = await _masterData.GetAllMasterKeysAsync();

            return View(new MasterValuesViewModel
            {
                MasterValues = new List<MasterDataValueViewModel>(),
                MasterValueInContext = new MasterDataValueViewModel(),
                IsEdit = false
            });
        }

        [HttpGet]
        public async Task<IActionResult> MasterValuesByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Json(new { data = new List<MasterDataValue>() });
            }

            var values = await _masterData.GetAllMasterValuesByKeyAsync(key);
            return Json(new { data = values });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MasterValues(bool isEdit, MasterDataValueViewModel masterValue)
        {
            if (!isEdit)
            {
                ModelState.Remove(nameof(masterValue.RowKey));
            }

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Validation failed."
                });
            }

            var currentUser = HttpContext.User.GetCurrentUserDetails();
            var auditUser = currentUser.Email ?? currentUser.Name;
            var masterDataValue = _mapper.Map<MasterDataValue>(masterValue);

            if (isEdit)
            {
                masterDataValue.UpdatedBy = auditUser;
                masterDataValue.UpdatedDate = DateTime.UtcNow;

                await _masterData.UpdateMasterValueAsync(
                    masterDataValue.PartitionKey,
                    masterDataValue.RowKey,
                    masterDataValue
                );
            }
            else
            {
                masterDataValue.RowKey = Guid.NewGuid().ToString();
                masterDataValue.CreatedBy = auditUser;
                masterDataValue.CreatedDate = DateTime.UtcNow;
                masterDataValue.UpdatedBy = auditUser;
                masterDataValue.UpdatedDate = DateTime.UtcNow;
                masterDataValue.IsDeleted = false;

                await _masterData.InsertMasterValueAsync(masterDataValue);
            }

            return Json(new
            {
                success = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadExcel()
        {
            var files = Request.Form.Files;

            if (files == null || !files.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Please choose an Excel file."
                });
            }

            var excelFile = files.First();

            if (excelFile == null || excelFile.Length <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "The selected file is empty."
                });
            }

            var extension = Path.GetExtension(excelFile.FileName);
            if (string.IsNullOrWhiteSpace(extension) || (extension != ".xlsx" && extension != ".xls"))
            {
                return Json(new
                {
                    success = false,
                    message = "Only Excel files (.xls, .xlsx) are allowed."
                });
            }

            var masterValues = await ParseMasterDataExcel(excelFile);

            if (!masterValues.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "No valid data found in the Excel file."
                });
            }

            var currentUser = HttpContext.User.GetCurrentUserDetails();
            var auditUser = currentUser.Email ?? currentUser.Name;
            var now = DateTime.UtcNow;

            foreach (var item in masterValues)
            {
                item.CreatedBy = auditUser;
                item.CreatedDate = now;
                item.UpdatedBy = auditUser;
                item.UpdatedDate = now;
                item.IsDeleted = false;
            }

            var result = await _masterData.UploadBulkMasterData(masterValues);

            return Json(new
            {
                success = result,
                message = result ? "Upload successful." : "Upload failed."
            });
        }

       private async Task<List<MasterDataValue>> ParseMasterDataExcel(IFormFile excelFile)
{
    var masterValueList = new List<MasterDataValue>();

    using (var memoryStream = new MemoryStream())
    {
        await excelFile.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

        using (var package = new ExcelPackage(memoryStream))
        {
            if (package.Workbook.Worksheets.Count == 0)
            {
                return masterValueList;
            }

            ExcelWorksheet worksheet = package.Workbook.Worksheets[0];

            if (worksheet.Dimension == null)
            {
                return masterValueList;
            }

            int rowCount = worksheet.Dimension.Rows;
            int colCount = worksheet.Dimension.Columns;

            // Detect the starting column: find the first column in row 1 that has "MasterKey" header
            int startCol = 1;
            for (int col = 1; col <= colCount; col++)
            {
                var headerValue = worksheet.Cells[1, col].Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(headerValue) &&
                    headerValue.Equals("MasterKey", StringComparison.OrdinalIgnoreCase))
                {
                    startCol = col;
                    break;
                }
            }

            // If no "MasterKey" header found, try to find first non-empty column
            if (startCol == 1)
            {
                for (int col = 1; col <= colCount; col++)
                {
                    var headerValue = worksheet.Cells[1, col].Value?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(headerValue))
                    {
                        startCol = col;
                        break;
                    }
                }
            }

            for (int row = 2; row <= rowCount; row++)
            {
                var partitionKey = worksheet.Cells[row, startCol].Value?.ToString()?.Trim();
                var name = worksheet.Cells[row, startCol + 1].Value?.ToString()?.Trim();
                var isActiveText = worksheet.Cells[row, startCol + 2].Value?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(partitionKey) && string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                bool isActive = false;
                if (!string.IsNullOrWhiteSpace(isActiveText))
                {
                    var normalized = isActiveText.ToLower();

                    isActive =
                        normalized == "true" ||
                        normalized == "1" ||
                        normalized == "yes" ||
                        normalized == "y";
                }

                var masterDataValue = new MasterDataValue
                {
                    RowKey = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKey,
                    Name = name,
                    IsActive = isActive
                };

                masterValueList.Add(masterDataValue);
            }
        }
    }

    return masterValueList;
}
    }
}