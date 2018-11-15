using TimeSheetManagementSystem.Models;
using TimeSheetManagementSystem.Data;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TimeSheetManagementSystem.Services;
using Microsoft.Extensions.Logging;
using TimeSheetManagementSystem.Controllers;
using System.Globalization;
using Microsoft.AspNetCore.Http;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TimeSheetManagementSystem.APIs
{
    [Authorize("RequireAdminRole")]
    [Route("api/[controller]")]
    public class AccountDetailsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly ILogger _logger;
        public ApplicationDbContext Database { get; }
        public IConfigurationRoot Configuration { get; }
        public AccountDetailsController(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ISmsSender smsSender,
            ILoggerFactory loggerFactory, ApplicationDbContext database)
        {
            Database = database; //Initialize the Database property

            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _smsSender = smsSender;
            _logger = loggerFactory.CreateLogger<AccountController>();
        }
        // GET: api/<controller>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<controller>/5
        [HttpGet("{id}")]
        public List<object> Get(int id)
        {
            List<AccountDetail> accountDetails = Database.AccountDetails.Where(a => a.CustomerAccountId == id).OrderBy(t=>t.DayOfWeekNumber).ToList();
            List<AccountRate> accountRates = Database.AccountRates.Where(a => a.CustomerAccountId == id).OrderByDescending(a=>a.EffectiveStartDate).ToList();
            List<object> accountDetailsList = new List<Object>();
            //Array to include name of each day in a week
            //Day of week number can be used as index to get day name
            String[] daysInWeek = new String[] { "", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

            foreach (AccountDetail accountDetailObj in accountDetails)
            {
                int startTimeMinutes = accountDetailObj.StartTimeInMinutes;
                int endTimeMinutes = accountDetailObj.EndTimeInMinutes;
                string startTime = findTimeFromMinutes(startTimeMinutes);
                string endTime = findTimeFromMinutes(endTimeMinutes);
                

                Object accountDetailListObject = new
                {
                    id = accountDetailObj.AccountDetailId,
                    day = daysInWeek[accountDetailObj.DayOfWeekNumber],
                    startTime = startTime,
                    endTime = endTime,
                    startDate = accountDetailObj.EffectiveStartDate.ToString("dd/MM/yyyy"),
                    //Check if the end date object is null. If null, endDate = "n/a". Else, endDate = date in dd/MM/yyyy
                    endDate = accountDetailObj.EffectiveEndDate != null ? accountDetailObj.EffectiveEndDate.Value.ToString("dd/MM/yyyy") : "n/a",
                    visible = accountDetailObj.IsVisible
                };
                accountDetailsList.Add(accountDetailListObject);
            }
            return accountDetailsList;
        }

        [HttpGet("{id},{detailId}")]
        public Object GetOneDetail(int id, int detailId)
        {
            try
            {
                AccountDetail accountDetailObj = Database.AccountDetails.Where(a => a.CustomerAccountId == id).Where(a => a.AccountDetailId == detailId).First();
                //Array to include name of each day in a week
                //Day of week number can be used as index to get day name
                String[] daysInWeek = new String[] { "", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

                int startTimeMinutes = accountDetailObj.StartTimeInMinutes;
                int endTimeMinutes = accountDetailObj.EndTimeInMinutes;
                string startTime = findTimeFromMinutes(startTimeMinutes);
                string endTime = findTimeFromMinutes(endTimeMinutes);

                Object returnedAccountDetail = new
                {
                    id = accountDetailObj.AccountDetailId,
                    day = daysInWeek[accountDetailObj.DayOfWeekNumber],
                    startTime = startTime,
                    endTime = endTime,
                    startDate = accountDetailObj.EffectiveStartDate.ToString("dd/MM/yyyy"),
                    //dt2 != null ? dt2.Value.ToString("yyyy-MM-dd hh:mm:ss") : "n/a"
                    //Check if the end date object is null. If null, endDate = "n/a". Else, endDate = date in dd/MM/yyyy
                    endDate = accountDetailObj.EffectiveEndDate != null ? accountDetailObj.EffectiveEndDate.Value.ToString("dd/MM/yyyy") : "n/a",
                    visible = accountDetailObj.IsVisible
                };
                return returnedAccountDetail;
            }
            catch (Exception ex)
            {
                Object errorObject = new { detailId = detailId };
                return errorObject;
            }
        }

        // POST api/<controller>
        [HttpPost]
        public IActionResult Post(IFormCollection value)
        {
            string customMessage = "";
            int userId = GetUserIdFromUserInfo();
            AccountDetail newAccountDetail = new AccountDetail();
            string accountId = value["accountId"]; //get customerAccountId
            string customerName = "";
            String[] daysInWeek = new String[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            try //Create AccountDetail
            {

                newAccountDetail.CustomerAccountId = int.Parse(accountId);
                if (!Database.AccountRates.Where(t => t.CustomerAccountId == newAccountDetail.CustomerAccountId).ToList().Any())
                //if no account rates record
                {
                    customMessage = "Please create at least 1 account rate record!";
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
                List<AccountDetail> accountDetails = Database.AccountDetails.Where(a => a.CustomerAccountId == newAccountDetail.CustomerAccountId).ToList();
                //DateTime.ParseExact("24/01/2013", "dd/MM/yyyy", CultureInfo.InvariantCulture);
                newAccountDetail.EffectiveStartDate = DateTime.ParseExact(value["startDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture); //convert to DateTime obj
                if (value["endDate"] == "")
                {
                    newAccountDetail.EffectiveEndDate = null;
                }
                else
                {
                    newAccountDetail.EffectiveEndDate = DateTime.ParseExact(value["endDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture); //convert to DateTime obj

                }
                newAccountDetail.DayOfWeekNumber = Array.FindIndex(daysInWeek, currDay => currDay == value["dayOfWeek"]) + 1;
                newAccountDetail.StartTimeInMinutes = int.Parse(value["startTime"]);
                newAccountDetail.EndTimeInMinutes = int.Parse(value["endTime"]);
                newAccountDetail.IsVisible = Boolean.Parse(value["visibility"]);

                customerName = Database.CustomerAccounts.Where(t => t.CustomerAccountId == newAccountDetail.CustomerAccountId).First().AccountName;
                Database.AccountDetails.Add(newAccountDetail);
                Database.CustomerAccounts.Where(t => t.AccountName == customerName).First().UpdatedAt = DateTime.Now;
                Database.CustomerAccounts.Where(t => t.AccountName == customerName).First().UpdatedById = userId;
                //check if account detail is created before any account rate
                DateTime startDateAccountRate = Database.AccountRates.Where(t => t.CustomerAccountId == newAccountDetail.CustomerAccountId)
                    .OrderBy(t => t.EffectiveStartDate).First().EffectiveStartDate;
                if (DateTime.Compare(startDateAccountRate, newAccountDetail.EffectiveStartDate) > 0)
                {
                    customMessage = "Unable to save Account Detail record since start date of this account detail record is before start date of first account rate. (No account rate can be applied to this account detail)";
                    System.Diagnostics.Debug.WriteLine("Account Rate Id is " + newAccountDetail.CustomerAccountId);
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
                //checkOverlap(List<AccountDetail> accountDetails, DateTime startDate,DateTime endDate, int startTimeMin, int endTimeMin, int dayOfWeekNum)
                if (!checkOverlap(accountDetails,
                    newAccountDetail.EffectiveStartDate,
                    newAccountDetail.EffectiveEndDate,
                    newAccountDetail.StartTimeInMinutes,
                    newAccountDetail.EndTimeInMinutes,
                    newAccountDetail.DayOfWeekNumber))
                {
                    customMessage = "There already exists another record within same time period, same day, same date period!";
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
                Database.SaveChanges();
            }
            catch (Exception exceptionObject)
            {
                try
                {
                    if (exceptionObject.InnerException.Message
                          .Contains("AccountDetail_AccountDetailId_UniqueConstraint") == true)
                    {
                        customMessage = "Unable to save Account Detail record for " +
                                      customerName + ". Please contact your administrator (same Account Detail id)";
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                    else
                    {
                        customMessage = "Unable to save Account Rate record for unknown reasons. Please contact your administrator. Likely reasons are rate is only allowed up to 4 digits and 2 decimal numbers";
                        System.Diagnostics.Debug.WriteLine("Account Rate Id is " + newAccountDetail.CustomerAccountId);
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                catch
                {
                    customMessage = "Unable to save Account Rate record for unknown reasons.";
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
            }
            var successRequestResultMessage = new
            {
                message = "Saved Account Detail record"
            };
            //Create a OkObjectResult class instance, httpOkResult.
            //When creating the object, provide the previous message object into it.
            OkObjectResult httpOkResult =
                                new OkObjectResult(successRequestResultMessage);
            //Send the OkObjectResult class object back to the client.
            return httpOkResult;

        }

        // PUT api/<controller>/5
        [HttpPut("{detailId}")]
        public IActionResult Put(int detailId, IFormCollection value)
        {
            string customMessage = "";
            int userId = GetUserIdFromUserInfo();
            string accountId = value["accountId"]; //get customerAccountId
            string customerName = "";
            String[] daysInWeek = new String[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            try //Create AccountDetail
            {

                //fix code below
                AccountDetail newAccountDetail = Database.AccountDetails.Where(a => a.AccountDetailId == detailId).First();
                newAccountDetail.CustomerAccountId = int.Parse(accountId);
                if (!Database.AccountRates.Where(t => t.CustomerAccountId == newAccountDetail.CustomerAccountId).ToList().Any())
                //if no account rates record
                {
                    customMessage = "Please create at least 1 account rate record!";
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
                //List of accountDetails excluding this record
                List<AccountDetail> accountDetails = Database.AccountDetails.Where(a => a.CustomerAccountId == newAccountDetail.CustomerAccountId)
                    .Where(a => a.AccountDetailId != detailId).ToList();//.Where(a => a.AccountDetailId != detailId)
                if (value["endDate"] == "")
                {
                    if (!checkOverlap(accountDetails,
                     DateTime.ParseExact(value["startDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture),
                     null,
                     int.Parse(value["startTime"]),
                     int.Parse(value["endTime"]),
                     Array.FindIndex(daysInWeek, currDay => currDay == value["dayOfWeek"]) + 1))
                    {
                        customMessage = "There already exists another record within same time period, same day, same date period!";
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                else
                {
                    if (!checkOverlap(accountDetails,
                    DateTime.ParseExact(value["startDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    DateTime.ParseExact(value["endDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    int.Parse(value["startTime"]),
                    int.Parse(value["endTime"]),
                    Array.FindIndex(daysInWeek, currDay => currDay == value["dayOfWeek"]) + 1))
                    {
                        customMessage = "There already exists another record within same time period, same day, same date period!";
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                
                //DateTime.ParseExact("24/01/2013", "dd/MM/yyyy", CultureInfo.InvariantCulture);
                newAccountDetail.EffectiveStartDate = DateTime.ParseExact(value["startDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture); //convert to DateTime obj
                if (value["endDate"] == "")
                {
                    newAccountDetail.EffectiveEndDate = null;
                }
                else
                {
                    newAccountDetail.EffectiveEndDate = DateTime.ParseExact(value["endDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture); //convert to DateTime obj

                }
                newAccountDetail.DayOfWeekNumber = Array.FindIndex(daysInWeek, currDay => currDay == value["dayOfWeek"]) + 1;
                newAccountDetail.StartTimeInMinutes = int.Parse(value["startTime"]);
                newAccountDetail.EndTimeInMinutes = int.Parse(value["endTime"]);
                newAccountDetail.IsVisible = Boolean.Parse(value["visibility"]);
                customerName = Database.CustomerAccounts.Where(t => t.CustomerAccountId == newAccountDetail.CustomerAccountId).First().AccountName;
                //Database.AccountDetails.Add(newAccountDetail);
                Database.CustomerAccounts.Where(t => t.AccountName == customerName).First().UpdatedAt = DateTime.Now;
                Database.CustomerAccounts.Where(t => t.AccountName == customerName).First().UpdatedById = userId;
                //check if account detail is created before any account rate
                DateTime startDateAccountRate = Database.AccountRates.Where(t => t.CustomerAccountId == newAccountDetail.CustomerAccountId)
                    .OrderBy(t => t.EffectiveStartDate).First().EffectiveStartDate;

                if (DateTime.Compare(startDateAccountRate, newAccountDetail.EffectiveStartDate) > 0)
                {
                    customMessage = "Unable to save Account Detail record since start date of this account detail record is before start date of first account rate. (No account rate can be applied to this account detail)";
                    System.Diagnostics.Debug.WriteLine("Account Rate Id is " + newAccountDetail.CustomerAccountId);
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
                //checkOverlap(List<AccountDetail> accountDetails, DateTime startDate,DateTime endDate, int startTimeMin, int endTimeMin, int dayOfWeekNum)
                //check if this record overlaps with any other record

                Database.SaveChanges();
            }
            catch (Exception exceptionObject)
            {
                try
                {
                    if (exceptionObject.InnerException.Message
                          .Contains("AccountDetail_AccountDetailId_UniqueConstraint") == true)
                    {
                        customMessage = "Unable to save Account Detail record for " +
                                      customerName + ". Please contact your administrator (same Account Detail id)";
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                    else
                    {
                        customMessage = "Unable to save Account Detail record for unknown reasons. Please contact your administrator. Likely reasons are rate is only allowed up to 4 digits and 2 decimal numbers";
                        //System.Diagnostics.Debug.WriteLine("Account Rate Id is " + newAccountDetail.CustomerAccountId);
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                catch
                {
                    customMessage = "Unable to save Account Detail record for unknown reasons.";
                    //System.Diagnostics.Debug.WriteLine("Account Rate Id is " + newAccountDetail.CustomerAccountId);
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
         
            }
            var successRequestResultMessage = new
            {
                message = "Saved Account Detail record"
            };
            //Create a OkObjectResult class instance, httpOkResult.
            //When creating the object, provide the previous message object into it.
            OkObjectResult httpOkResult =
                                new OkObjectResult(successRequestResultMessage);
            //Send the OkObjectResult class object back to the client.
            return httpOkResult;

        }

        // DELETE api/<controller>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
        public int GetUserIdFromUserInfo()
        {
            string userLoginId = _userManager.GetUserName(User);
            int userInfoId = Database.UserInfo.Single(input => input.LoginUserName == userLoginId).UserInfoId;
            return userInfoId;
        }
        public string findTimeFromMinutes(int totalMinutes)
        {
            //total hours
            int hours = totalMinutes / 60;
            string meridiem = "am";
            if (hours >= 12)
            {
                meridiem = "pm";
                if (hours > 12)
                {
                    hours = hours - 12;
                }
            }
            else if (hours == 0)
            {
                hours = 12; //0h30min -> 12.30am
            }

            //total minutes
            int minutes = totalMinutes % 60;
            if (totalMinutes == 1440)//2400 hrs
            {
                hours = 12;
                minutes = 0;
                meridiem = "am";
            }
            //output is 1:10
            return string.Format("{0}.{1:00}{2}", hours, minutes, meridiem);
        }
        public Boolean checkOverlap(List<AccountDetail> accountDetails, DateTime startDate, DateTime? endDate,
            int startTimeMin, int endTimeMin, int dayOfWeekNum)
        {
            foreach (AccountDetail detail in accountDetails)
            {
                bool overlapDate = false;
                if (detail.EffectiveEndDate != null && endDate != null)
                {
                     overlapDate = (detail.EffectiveStartDate < endDate && startDate < detail.EffectiveEndDate) || (detail.EffectiveStartDate == startDate && detail.EffectiveEndDate == endDate);
                }
                else if (detail.EffectiveEndDate != null && endDate == null)
                {
                    overlapDate = (startDate < detail.EffectiveEndDate);
                }
                else if (detail.EffectiveEndDate == null && endDate != null)
                {
                    overlapDate = (detail.EffectiveStartDate < endDate);
                }
                else if (detail.EffectiveEndDate == null && endDate == null)
                {
                    overlapDate = true;
                }
                if (overlapDate)
                    {
                        if (detail.DayOfWeekNumber == dayOfWeekNum)
                        {
                            // check if day overlaps
                            bool overlapMin = (detail.StartTimeInMinutes < endTimeMin && startTimeMin < detail.EndTimeInMinutes)
                                || (detail.StartTimeInMinutes == startTimeMin && detail.EndTimeInMinutes == endTimeMin);

                            //System.Diagnostics.Debug.WriteLine("LOK::");
                            //System.Diagnostics.Debug.WriteLine(overlapMin.ToString());
                            if (overlapMin)
                            {

                                return false; //if time overlaps, overlapMin == true
                            }
                        }
                    }
                }

            return true;
        }
        
    }
}
