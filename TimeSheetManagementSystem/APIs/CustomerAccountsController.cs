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
using Microsoft.AspNetCore.Http;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TimeSheetManagementSystem.APIs
{
    [Authorize("RequireAdminRole")]
    [Route("api/[controller]")]
    public class CustomerAccountsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly ILogger _logger;
        private int global = 0;
        public ApplicationDbContext Database { get; }
        public IConfigurationRoot Configuration { get; }
        // GET: api/<controller>
        public CustomerAccountsController(UserManager<ApplicationUser> userManager,
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
        public JsonResult Get()
        {
            List<object> customerAccountsList = new List<object>();
            var customerQueryResult = Database.CustomerAccounts.Include(t => t.UpdatedBy).Include(t=>t.InstructorAccounts).Include(t=>t.AccountRates).OrderByDescending(t=>t.UpdatedAt).ToList();
            foreach (var oneCustomerAccounts in customerQueryResult)
            {
                customerAccountsList.Add(new
                {
                    //Should try and recreate the db modeling codes instead
                    accountName = oneCustomerAccounts.AccountName,
                    noOfRatesData = oneCustomerAccounts.AccountRates.Count(),
                    noOfInstructors = oneCustomerAccounts.InstructorAccounts.Count(),
                    comments = oneCustomerAccounts.Comments,
                    accountId = oneCustomerAccounts.CustomerAccountId,
                    updatedBy = oneCustomerAccounts.UpdatedBy.FullName,
                    updatedAt = oneCustomerAccounts.UpdatedAt,
                    visibility = oneCustomerAccounts.IsVisible

                });
            }//end of foreach
            return new JsonResult(customerAccountsList);
        }

        // GET api/<controller>/5
        [HttpGet("accountName/{id}")]
        public IActionResult Get(int id)
        {
            List<object> customerAccountsList = new List<object>();
            List<object> rateList = new List<object>();
            var customerQueryResult = Database.CustomerAccounts.Include(t => t.AccountRates)
                .Where(t => t.CustomerAccountId == id).First();
            customerAccountsList.Add(new
            {
                //Should try and recreate the db modeling codes instead
                accountName = customerQueryResult.AccountName
                //accountId = customerQueryResult.CustomerAccountId

            });
            //System.Diagnostics.Debug.WriteLine("For self:");
            //System.Diagnostics.Debug.WriteLine(customerAccountsList[0]);
            return new JsonResult(customerAccountsList);
        }
        [HttpGet("accountId/{name}")]
        public IActionResult GetAccountName(string name)
        {
            List<object> customerAccountsList = new List<object>();
            List<object> rateList = new List<object>();
            var customerQueryResult = Database.CustomerAccounts.Include(t => t.AccountRates)
                .Where(t => t.AccountName == name).First();
            customerAccountsList.Add(new
            {
                //Should try and recreate the db modeling codes instead
                accountId = customerQueryResult.CustomerAccountId
                //accountId = customerQueryResult.CustomerAccountId

            });
            //System.Diagnostics.Debug.WriteLine("For self:");
            //System.Diagnostics.Debug.WriteLine(customerAccountsList[0]);
            return new JsonResult(customerAccountsList);
        }
        [HttpGet("getCustomerUpdateInfo/{id}")]
        public IActionResult GetInfoForUpdate(int id)
        {
            List<object> customerAccountsList = new List<object>();
            List<object> rateList = new List<object>();
            var customerQueryResult = Database.CustomerAccounts.Include(t => t.AccountRates).Where(t => t.CustomerAccountId == id).First();
            //List<AccountRate> accountRateList = Database.AccountRates.Include(t => t.CustomerAccount).Where(t => t.CustomerAccountId == id).ToList();
            //if (accountRateList.Count() > 0) {
                customerAccountsList.Add(new
                {
                    //Should try and recreate the db modeling codes instead
                    accountName = customerQueryResult.AccountName,
                    accountId = customerQueryResult.CustomerAccountId,
                    comment = customerQueryResult.Comments,
                    isVisible = customerQueryResult.IsVisible,
                    warning = "no"

                });
            //}
            //else { 
            //}
            //System.Diagnostics.Debug.WriteLine("For self:");
            //System.Diagnostics.Debug.WriteLine(customerAccountsList[0]);
            return new JsonResult(customerAccountsList);
        }

        // POST api/<controller>
        [HttpPost]
        public IActionResult Post(IFormCollection value)
        {
            string customMessage = "";
            
            /*
            object httpFailRequestResultMessage = new { message = "abc" };
            //Return a bad http request message to the client
            return BadRequest(httpFailRequestResultMessage);
            */
            int userId = GetUserIdFromUserInfo();
            
            //dynamic customerNewInput = JsonConvert.DeserializeObject<dynamic>(value);
            
            CustomerAccount newCustomer = new CustomerAccount();
            
            //create customer account first
            try
            {
                
                DateTime currentTime = DateTime.Now;
                newCustomer.CreatedById = userId;
                newCustomer.UpdatedById = userId;
                newCustomer.AccountName = value["accountName"];
                newCustomer.CreatedAt = currentTime;
                newCustomer.UpdatedAt = currentTime;
                newCustomer.Comments = value["comment"];
                newCustomer.IsVisible = Boolean.Parse(value["isVisible"]);
                Database.CustomerAccounts.Add(newCustomer);
                Database.SaveChanges();
            }
            catch (Exception exceptionObject)
            {
                //System.Diagnostics.Debug.WriteLine("LJ");
                //System.Diagnostics.Debug.WriteLine(exceptionObject.Message);

                System.Diagnostics.Debug.WriteLine("Error Log here.");
                System.Diagnostics.Debug.WriteLine(exceptionObject.Message);
                System.Diagnostics.Debug.WriteLine(userId);
                //System.Diagnostics.Debug.WriteLine(customerNewInput.accountName.Value);
                //System.Diagnostics.Debug.WriteLine(customerNewInput.visibility.Value);
                try
                {
                    if (exceptionObject.InnerException.Message
                              .Contains("CustomerAccount_AccountName_UniqueConstraint") == true)
                    {

                        customMessage = "Unable to save Customer record due " +
                                      "to another record having the same name : " +
                                      newCustomer.AccountName;
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);
                    }
                    else
                    {
                        customMessage = "Failed due to unknown reasons";
                        object httpFailRequestResultMessage = new { message = customMessage };
                        return BadRequest(httpFailRequestResultMessage);

                    }
                }
                catch
                {
                    customMessage = "Failed due to unknown reasons";
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }

            }//End of Try..Catch block
            System.Diagnostics.Debug.WriteLine("HereOk4");
            var successRequestResultMessage = new
            {
                message = "Saved Customer Account record"
            };
            //Create a OkObjectResult class instance, httpOkResult.
            //When creating the object, provide the previous message object into it.
            OkObjectResult httpOkResult =
                                new OkObjectResult(successRequestResultMessage);
            //Send the OkObjectResult class object back to the client.
            return httpOkResult;
        }

        // PUT api/<controller>/5
        [HttpPut("{id}")] //customer account id
        public IActionResult Put(int id, IFormCollection value)
        {
            var customMessage = "";
            //dynamic customerNewUpdate = JsonConvert.DeserializeObject<dynamic>(value);
            int userId = GetUserIdFromUserInfo();
            CustomerAccount oneCustomerAccount = Database.CustomerAccounts.Where(t => t.CustomerAccountId == id).First();
            System.Diagnostics.Debug.WriteLine("Here are the errors");
            try
            {
                oneCustomerAccount.AccountName = value["accountName"];
                oneCustomerAccount.IsVisible = Boolean.Parse(value["isVisible"]);
                oneCustomerAccount.UpdatedById = userId;
                oneCustomerAccount.UpdatedAt = DateTime.Now;
                oneCustomerAccount.Comments = value["comment"];
                Database.SaveChanges();
                System.Diagnostics.Debug.WriteLine("Here are the errors");
                customMessage = "Successfully updated new customer record info!";

            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Here are the errors333");
                //System.Diagnostics.Debug.WriteLine(ex);
                customMessage = "Error updating data. This account name may have been used before.";
                object httpFailRequestResultMessage = new { message = customMessage };
                return BadRequest(httpFailRequestResultMessage);
            }
            var successRequestResultMessage = new
            {
                message = customMessage
            };
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

    }
}
