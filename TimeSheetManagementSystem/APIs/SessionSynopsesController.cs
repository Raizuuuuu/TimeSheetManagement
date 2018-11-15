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
    public class SessionSynopsesController : Controller
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
        public SessionSynopsesController(UserManager<ApplicationUser> userManager,
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
        [HttpGet]
        public JsonResult Get()
        {
            List<object> sessionSypnosisList = new List<object>();
            var sessionQueryResult = Database.SessionSynopses.Include(t=>t.CreatedBy).Include(t=>t.UpdatedBy).ToList();
            //       .Where(eachSession => eachSession.DeletedAt == null);
            foreach (var oneSessionSypnosis in sessionQueryResult)
            {
                sessionSypnosisList.Add(new
                {
                    //Should try and recreate the db modeling codes instead
                    sessionSypnosisId = oneSessionSypnosis.SessionSynopsisId,
                    createdBy = oneSessionSypnosis.CreatedBy.FullName,
                    updatedBy = oneSessionSypnosis.UpdatedBy.FullName,
                    isVisible = oneSessionSypnosis.IsVisible,
                    sessionSypnosisName = oneSessionSypnosis.SessionSynopsisName
                });
            }//end of foreach
            return new JsonResult(sessionSypnosisList);
        }//end of Get()

        // GET api/<controller>/5
        [HttpGet("{id}")]
        public JsonResult Get(int id)
        {
            try
            {
                var oneSessionSypnosis = Database.SessionSynopses.Where(x => x.SessionSynopsisId == id).First();
                Object sessionSypnosisObj = new
                {
                    isVisible = oneSessionSypnosis.IsVisible,
                    sessionSypnosisName = oneSessionSypnosis.SessionSynopsisName
                };
                return new JsonResult(sessionSypnosisObj);
            }
            catch
            {
                return new JsonResult(new { isError = "true" });
            }
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
            //Reconstruct a useful object from the input string value. 
            //dynamic sessionNewInput = JsonConvert.DeserializeObject<dynamic>(value);
            SessionSynopsis newSession = new SessionSynopsis();
            try
            {
                
                newSession.CreatedById = userId;
                newSession.UpdatedById = userId;
                //newSession.SessionSynopsisName = sessionNewInput.sessionSynopsisName.Value;
                newSession.SessionSynopsisName = value["sessionSynopsisName"];
                //System.Diagnostics.Debug.WriteLine("Lok Message: ok here 111");
                //System.Diagnostics.Debug.WriteLine(newSession.SessionSynopsisName);
                newSession.IsVisible = Boolean.Parse(value["visibility"]);
                //When I add this Course instance, newCourse into the
                //Courses Entity Set, it will turn into a Course entity waiting to be mapped
                //as a new record inside the actual Course table.
                Database.SessionSynopses.Add(newSession);

                Database.SaveChanges();//Telling the database model to save the changes
            }
            catch (Exception exceptionObject)
            {
                System.Diagnostics.Debug.WriteLine("LJ");
                //System.Diagnostics.Debug.WriteLine(exceptionObject.Message);
                try
                {
                    if (exceptionObject.InnerException.Message
                              .Contains("SessionSynopsis_SessionSynopsisName_UniqueConstraint") == true)
                    {
                        System.Diagnostics.Debug.WriteLine(newSession.SessionSynopsisName);
                        customMessage = "Unable to save Session Sypnosis record due " +
                                      "to another record having the same name : " +
                                      newSession.SessionSynopsisName;
                        //Create an anonymous type object that has one property, message.
                        //This anonymous object's message property contains a simple string message
                        object httpFailRequestResultMessage = new { message = customMessage };
                        //Return a bad http request message to the client
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                catch
                {
                    customMessage = "Unable to save Session Sypnosis record due to unknown reasons";
                    object httpFailRequestResultMessage = new { message = customMessage };
                    return BadRequest(httpFailRequestResultMessage);
                }
            }//End of Try..Catch block

            //If there is no runtime error in the try catch block, the code execution
            //should reach here. Sending success message back to the client.

            //******************************************************
            //Construct a custom message for the client
            //Create a success message anonymous type object which has a 
            //message member variable (property)
            var successRequestResultMessage = new
            {
                message = "Saved Session Sypnosis record"
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
            string customMessage = "";
            int userId = GetUserIdFromUserInfo();
            //var sessionSynopsisChangeInput = JsonConvert.DeserializeObject<dynamic>(value);
            //After reconstructing the object from the JSON string residing 
            //in the input parameter variable, value:
            //To obtain the course Abbreviation information, 
            //use courseChangeInput.courseAbbreviation.Value
            //To obtain the course name information, 
            //use courseChangeInput.courseName.Value
            var oneSessionSypnosis = Database.SessionSynopses
                .Where(sessionEntity => sessionEntity.SessionSynopsisId == id).Single();
            oneSessionSypnosis.IsVisible = Boolean.Parse(value["visibility"]);
            oneSessionSypnosis.SessionSynopsisName = value["sessionSynopsisName"];
            oneSessionSypnosis.UpdatedById = userId;
            try
            {
                Database.SaveChanges();
            }
            catch (Exception ex)
            {
                try
                {
                    if (ex.InnerException.Message
                      .Contains("SessionSynopsis_SessionSynopsisName_UniqueConstraint") == true)
                    {
                        customMessage = "Unable to save session record due " +
                             "to another Session Synopsis record having the same name as : " +
                        value["sessionSynopsisName"];
                        //Create an anonymous object that has one property, Message.
                        //This anonymous object's Message property contains a simple string message
                        object httpFailRequestResultMessage = new { message = customMessage };
                        //Return a bad http request message to the client
                        return BadRequest(httpFailRequestResultMessage);
                    }
                }
                catch
                {
                    customMessage = "Unable to save session record due to unknown reason";
                    object httpFailRequestResultMessage = new { message = customMessage };
                    //Return a bad http request message to the client
                    return BadRequest(httpFailRequestResultMessage);
                }
            }//End of try .. catch block on saving data
             //Construct a custom message for the client
             //Create a success message anonymous object which has a 
             //Message member variable (property)
            var successRequestResultMessage = new
            {
                message = "Saved Session Synopsis record"
            };

            //Create a OkObjectResult class instance, httpOkResult.
            //When creating the object, provide the previous message object into it.
            OkObjectResult httpOkResult =
                                    new OkObjectResult(successRequestResultMessage);
            //Send the OkObjectResult class object back to the client.
            return httpOkResult;
        }//End of Put() Web API method

        // DELETE api/<controller>/5
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            string customMessage = "";
            try
            {
                var foundOneSessionSynopses = Database.SessionSynopses
                        .Single(eachSessionSynopses => eachSessionSynopses.SessionSynopsisId == id);
                //Tell the db model to commit/persist the changes to the database, 
                //I use the following command.
                Database.Remove(foundOneSessionSynopses);
                Database.SaveChanges();
            }
            catch (Exception ex)
            {
                customMessage = "Unable to delete session record.";
                object httpFailRequestResultMessage = new { message = customMessage };
                //Return a bad http request message to the client
                return BadRequest(httpFailRequestResultMessage);
            }//End of try .. catch block on manage data

            //Build a custom message for the client
            //Create a success message anonymous object which has a 
            //Message member variable (property)
            var successRequestResultMessage = new
            {
                message = "Deleted session record"
            };

            //Create a OkObjectResult class instance, httpOkResult.
            //When creating the object, provide the previous message object into it.
            OkObjectResult httpOkResult =
                         new OkObjectResult(successRequestResultMessage);
            //Send the OkObjectResult class object back to the client.
            return httpOkResult;
        }

        public bool checkChanges()
        {
            //returns true if there is changes
            return false;
        }

        

        public int GetUserIdFromUserInfo()
        {
            string userLoginId = _userManager.GetUserName(User);
            int userInfoId = Database.UserInfo.Single(input => input.LoginUserName == userLoginId).UserInfoId;
            return userInfoId;
        }
    }
}

