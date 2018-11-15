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
using System.Text.RegularExpressions;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TimeSheetManagementSystem.APIs
{
    [Authorize("RequireAdminRole")]
    [Route("api/[controller]")]
    public class AccountRatesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly ILogger _logger;
        public ApplicationDbContext Database { get; }
        public IConfigurationRoot Configuration { get; }
        public AccountRatesController(UserManager<ApplicationUser> userManager,
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
        [HttpGet("customerId={id}")]
        public List<object> Get(int id)
        {
            List<object> accountRateList = new List<Object>();
            var rateQueryResult = Database.AccountRates.Where(t => t.CustomerAccountId == id).OrderByDescending(t => t.EffectiveStartDate).ToList();
            Boolean isRed = false;
            DateTime? previousDate = null;
            foreach (AccountRate accountRate in rateQueryResult)
            {
                accountRateList.Add(new
                {
                    accountRateId = accountRate.AccountRateId,
                    accountRate = string.Format("{0:C}", accountRate.RatePerHour),
                    effectiveStartDate = accountRate.EffectiveStartDate
                    //effectiveEndDate = accountRate.EffectiveEndDate
                }

            );
            }

            return accountRateList;
        }
        [HttpGet("{id},{customerName}")]
        public IActionResult Get(int id, string customerName)
        {
            List<object> accountRateList = new List<Object>();
            var accountRate = Database.AccountRates.Include(t => t.CustomerAccount)
                .Where(t => t.CustomerAccount.AccountName == customerName)
                .Where(t => t.AccountRateId == id).First();
            accountRateList.Add(new
            {
                ratePerHour = accountRate.RatePerHour,
                effectiveStartDate = accountRate.EffectiveStartDate
            });
            System.Diagnostics.Debug.WriteLine(accountRateList);
            return new JsonResult(accountRateList);
        }

        [HttpGet("CheckValidInputCreate/{customerName},{startDate}")]
        public IActionResult GetAboveAndBelow(string customerName, string startDate)
        {
            List<object> accountRateList = new List<Object>();
            //Dont forget to change - to /
            DateTime startDateObj = DateTime.ParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture);
            var accountRateQuery = Database.AccountRates.Include(t => t.CustomerAccount)
                .Where(t => t.CustomerAccount.AccountName == customerName).OrderByDescending(t => t.EffectiveStartDate)
                .ToList();
            int accountRateQueryLength = accountRateQuery.Count();
            bool isBigger = true;
            AccountRate previousObj = new AccountRate();
            for (int i = 0; i < accountRateQueryLength; i++)
            {
                AccountRate rateObj = accountRateQuery[i];
                int result = DateTime.Compare(startDateObj, rateObj.EffectiveStartDate);
                System.Diagnostics.Debug.WriteLine("Here:" + result);
                //if start date input is above all records
                //if start date input is equals to a record
                if (result == 0)
                {
                    accountRateList = new List<Object>();
                    accountRateList.Add(new
                    {
                        ratePerHour = rateObj.RatePerHour,
                        effectiveStartDate = rateObj.EffectiveStartDate,
                        message = "There already exists another account rate record that starts on " + rateObj.EffectiveStartDate.ToString(),
                        status = "Failure",
                        rateId = rateObj.AccountRateId
                    }
                    );
                    return new JsonResult(accountRateList);
                }
                //if start date input is less than a record
                else
                {
                }
            }
            accountRateList.Add(new
            {
                message = "",
                status = "Success"
            }
                    );
            System.Diagnostics.Debug.WriteLine(accountRateList);
            return new JsonResult(accountRateList);
        }

        // POST api/<controller>
        [HttpPost]
        public IActionResult Post(IFormCollection value)
        {
            string customMessage = "";
            int userId = GetUserIdFromUserInfo();
            //dynamic accountRateNewInput = JsonConvert.DeserializeObject<dynamic>(value);
            AccountRate newAccountRate = new AccountRate();
            string accountName = value["accountName"];
            //System.Diagnostics.Debug.WriteLine("aaaaa");
            //bool confirmed = accountRateNewInput.confirm.Value;
            //System.Diagnostics.Debug.WriteLine(confirmed);
            /*
            if (!confirmed)
            {
                //Check start date and start date
                //Check if effective end date overlaps with any start date
                //Check if effective start date overlaps with any end date
                var customerQueryResult = Database.CustomerAccounts.Include(t => t.AccountRates).Where(t => t.AccountName == accountName).First();
                List<AccountRate> accountRates = customerQueryResult.AccountRates.OrderByDescending(t => t.EffectiveStartDate).ToList();
                bool isLatest = false;
                DateTime effectiveStartDate = DateTime.ParseExact(accountRateNewInput.startDate.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture); //convert to DateTime obj
                DateTime effectiveEndDate;
                DateTime previousStartDate;
                bool nullEndDate = false;
                if (accountRateNewInput.endDate.Value.Replace(" ", string.Empty) != string.Empty)
                    effectiveEndDate = (DateTime)(DateTime.ParseExact(accountRateNewInput.endDate.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture)); //convert to DateTime obj
                else
                    nullEndDate = true;
                foreach (AccountRate i in accountRates)
                {
                    //starts from latest date
                    //System.Diagnostics.Debug.WriteLine("aaaaa");
                    //System.Diagnostics.Debug.WriteLine(i.EffectiveStartDate);
                    int result = DateTime.Compare(effectiveStartDate, i.EffectiveStartDate);
                    if (result < 0)
                    {
                        isLatest = false;
                        previousStartDate = i.EffectiveStartDate;
                    }
                    else if (result == 0)
                    {
                        object httpFailRequestResultMessage = new { message = "There already exists another account rate with same start date" };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                    else
                    {
                        isLatest = true;
                        if (nullEndDate)
                        {
                            object httpFailRequestResultMessage = new { message = "Upon " + previousStartDate };
                            return BadRequest(httpFailRequestResultMessage);
                        }
                        else if ()
                        {

                        }
                    }

                }
            }
            */
            try //Create AccountRate
            {
                var customerQueryResult = Database.CustomerAccounts.Where(t => t.AccountName == accountName).First();
                System.Diagnostics.Debug.WriteLine("herlok");
                System.Diagnostics.Debug.WriteLine(customerQueryResult);
                newAccountRate.CustomerAccountId = customerQueryResult.CustomerAccountId;
                //DateTime.ParseExact("24/01/2013", "dd/MM/yyyy", CultureInfo.InvariantCulture);
                newAccountRate.EffectiveStartDate = DateTime.ParseExact(value["startDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture); //convert to DateTime obj
                //newAccountRate.EffectiveEndDate = null;
                    newAccountRate.RatePerHour = System.Convert.ToDecimal(value["ratePerHour"]);//convert to decimal
                Database.AccountRates.Add(newAccountRate);
                Database.CustomerAccounts.Where(t => t.AccountName == accountName).First().UpdatedAt = DateTime.Now;
                Database.CustomerAccounts.Where(t => t.AccountName == accountName).First().UpdatedById = userId;
                Database.SaveChanges();
            }
            catch (Exception exceptionObject)
            {
                System.Diagnostics.Debug.WriteLine("herlok");
                System.Diagnostics.Debug.WriteLine(exceptionObject);
                try
                {
                    if (exceptionObject.InnerException.Message
                          .Contains("AccountRate_AccountRateId_UniqueConstraint") == true)
                    {
                        customMessage = "Unable to save Account Rate record for " +
                                      accountName + ". Please contact your administrator (same Account Rate id)";
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                    else
                    {
                        customMessage = "Unable to save Account Rate record for unknown reasons. Please contact your administrator. Likely reasons are rate is only allowed up to 4 digits and 2 decimal numbers";
                        System.Diagnostics.Debug.WriteLine("Account Rate Id is " + newAccountRate.CustomerAccountId);
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                catch
                {
                    customMessage = "Unable to save Account Rate record for unknown reasons. ";
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
            }
            var successRequestResultMessage = new
            {
                message = "Saved Account Rate record"
            };
            //Create a OkObjectResult class instance, httpOkResult.
            //When creating the object, provide the previous message object into it.
            OkObjectResult httpOkResult =
                                new OkObjectResult(successRequestResultMessage);
            //Send the OkObjectResult class object back to the client.
            return httpOkResult;

        }

        // PUT api/<controller>/5
        [HttpPut("{id}")]
        public IActionResult Put(int id, IFormCollection value)
        {
            //id = account rate id
            string customMessage = "";
            int userId = GetUserIdFromUserInfo();
            //dynamic accountRateNewInput = JsonConvert.DeserializeObject<dynamic>(value);
            string customerName = value["accountName"];
            string accountName = value["accountName"];
            try //Create AccountRate
            {

                CustomerAccount customerQuery = Database.CustomerAccounts.Where(t => t.AccountName == customerName).First();
                int customerId = customerQuery.CustomerAccountId;
                
                AccountRate rateQueryResult = Database.AccountRates.Where(t => t.CustomerAccountId == customerId).Where(t => t.AccountRateId == id).First();
                //DateTime.ParseExact("24/01/2013", "dd/MM/yyyy", CultureInfo.InvariantCulture);
                rateQueryResult.EffectiveStartDate = DateTime.ParseExact(value["startDate"], "dd/MM/yyyy", CultureInfo.InvariantCulture); //convert to DateTime obj

                    //rateQueryResult.EffectiveEndDate = null;
                rateQueryResult.RatePerHour = System.Convert.ToDecimal(value["ratePerHour"]);//convert to decimal

                customerQuery.UpdatedAt = DateTime.Now;
                customerQuery.UpdatedById = userId;
                System.Diagnostics.Debug.WriteLine("herlok");
                System.Diagnostics.Debug.WriteLine(rateQueryResult.EffectiveStartDate);
                //System.Diagnostics.Debug.WriteLine(rateQueryResult.EffectiveEndDate);
                System.Diagnostics.Debug.WriteLine(rateQueryResult.RatePerHour);
                //check if any account detail start time is before this account rate
                //check if account rate is the earliest
                List<AccountDetail> accountDetails = Database.AccountDetails.Where(t => t.CustomerAccountId == customerId).ToList();
                try
                {
                    DateTime earliestStartDate = Database.AccountRates.Where(t => t.CustomerAccountId == customerId)
                        .Where(t => t.AccountRateId != id).OrderBy(t => t.EffectiveStartDate).Select(t => t.EffectiveStartDate).First();
                    if (rateQueryResult.EffectiveStartDate < earliestStartDate)
                    {
                        foreach (AccountDetail accountDetail in accountDetails)
                        {
                            if (accountDetail.EffectiveStartDate < rateQueryResult.EffectiveStartDate)
                            {
                                customMessage = "Unable to save Account Rate record since there exists an account detail record before this account rate start date ";

                                object httpFailRequestResultMessage = new { message = customMessage };
                                return BadRequest(httpFailRequestResultMessage);
                            }
                        }
                    }
                }
                catch //if there is no other earliestStartDate
                {
                    foreach (AccountDetail accountDetail in accountDetails)
                    {
                        if (accountDetail.EffectiveStartDate < rateQueryResult.EffectiveStartDate)
                        {
                            customMessage = "Unable to save Account Rate record since there exists an account detail record before this account rate start date ";

                            object httpFailRequestResultMessage = new { message = customMessage };
                            return BadRequest(httpFailRequestResultMessage);
                        }
                    }
                }
                

                Database.SaveChanges();
                }
                catch (Exception exceptionObject)
                {
                //System.Diagnostics.Debug.WriteLine("herlok");
                //System.Diagnostics.Debug.WriteLine(exceptionObject);
                try
                {
                    if (exceptionObject.InnerException.Message
                          .Contains("AccountRate_AccountRateId_UniqueConstraint") == true)
                    {
                        customMessage = "Unable to save Account Rate record for " +
                                      accountName + ". Please contact your administrator (same Account Rate id)";
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                    else
                    {
                        customMessage = "Unable to save Account Rate record for unknown reasons. Please contact your administrator";
                        System.Diagnostics.Debug.WriteLine("Account Rate Id is " + accountName);
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                catch
                {
                    customMessage = "Unable to save Account Rate record for unknown reasons.";
                    System.Diagnostics.Debug.WriteLine("Account Rate Id is " + accountName);
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
                }
            var successRequestResultMessage = new
            {
                message = "Saved Account Rate record"
            };
            //Create a OkObjectResult class instance, httpOkResult.
            //When creating the object, provide the previous message object into it.
            OkObjectResult httpOkResult =
                                new OkObjectResult(successRequestResultMessage);
            //Send the OkObjectResult class object back to the client.
            return httpOkResult;
        }

        // DELETE api/<controller>/5
        [HttpDelete("{id},{customerName}")]
        public IActionResult Delete(int id, string customerName)
        {
            string customMessage = "";
            try
            {
                // get customer Id from customerName
                CustomerAccount customerQuery = Database.CustomerAccounts.Where(t => t.AccountName == customerName).First();
                int customerId = customerQuery.CustomerAccountId;
                //check if there is any account detail record
                Boolean anyAccountDetail = Database.AccountDetails.Where(t => t.CustomerAccountId == customerId).Any();
                if (anyAccountDetail)
                {
                    DateTime earliestAccountDetail = Database.AccountDetails.Where(t => t.CustomerAccountId == customerId)
                           .OrderBy(t => t.EffectiveStartDate).Select(t => t.EffectiveStartDate).First();
                    DateTime thisStartDate = Database.AccountRates.Where(t => t.CustomerAccountId == customerId).Where(t => t.AccountRateId == id)
                        .Select(t => t.EffectiveStartDate).First();
                    DateTime earliestStartDate = Database.AccountRates.Where(t => t.CustomerAccountId == customerId).OrderBy(t => t.EffectiveStartDate)
                        .Select(t => t.EffectiveStartDate).First();
                    if (earliestAccountDetail >= thisStartDate && earliestStartDate == thisStartDate)
                    {
                        customMessage = "Unable to delete account rate record because it will cause one account detail record to have no rates at all";
                        object httpFailRequestResultMessage = new { message = customMessage };
                        //Return a bad http request message to the client
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                var foundOneAccountRate = Database.AccountRates.Include(t => t.CustomerAccount)
                        .Where(t => t.CustomerAccount.AccountName == customerName)
                        .Single(t => t.AccountRateId == id);
                //Tell the db model to commit/persist the changes to the database, 
                //I use the following command.
                Database.Remove(foundOneAccountRate);
                Database.SaveChanges();
            }
            catch (Exception ex)
            {
                customMessage = "Unable to delete account rate record.";
                object httpFailRequestResultMessage = new { message = customMessage };
                //Return a bad http request message to the client
                return BadRequest(httpFailRequestResultMessage);
            }//End of try .. catch block on manage data

            //Build a custom message for the client
            //Create a success message anonymous object which has a 
            //Message member variable (property)
            var successRequestResultMessage = new
            {
                message = "Deleted account rate record"
            };

            //Create a OkObjectResult class instance, httpOkResult.
            //When creating the object, provide the previous message object into it.
            OkObjectResult httpOkResult =
                         new OkObjectResult(successRequestResultMessage);
            //Send the OkObjectResult class object back to the client.
            return httpOkResult;
        }
        public int GetUserIdFromUserInfo()
        {
            string userLoginId = _userManager.GetUserName(User);
            int userInfoId = Database.UserInfo.Single(input => input.LoginUserName == userLoginId).UserInfoId;
            return userInfoId;
        }
    }
}
