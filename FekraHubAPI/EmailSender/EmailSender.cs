﻿using FekraHubAPI.Data;
using FekraHubAPI.Data.Models;
using FekraHubAPI.Repositories.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
namespace FekraHubAPI.EmailSender
{
    public class EmailSender : IEmailSender
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IRepository<Student> _studentRepo;
        private readonly IRepository<StudentContract> _studentContract;
        private readonly IRepository<SchoolInfo> _schoolInfo;
        private readonly IRepository<Course> _courseRepo;
        private readonly IConfiguration _configuration;
        public EmailSender(IRepository<SchoolInfo> schoolInfo, UserManager<ApplicationUser> userManager,
            IRepository<Student> studentRepo, IRepository<StudentContract> studentContract, ApplicationDbContext context,
            IConfiguration configuration, IRepository<Course> courseRepo)
        {
            _schoolInfo = schoolInfo;
            _userManager = userManager;
            _studentRepo = studentRepo;
            _studentContract = studentContract;
            _context = context;
            _configuration = configuration;
            _courseRepo = courseRepo;
        }

        private async Task<Task> SendEmail(string toEmail, string subject, string body, bool isBodyHTML, byte[]? pdf = null, string? pdfName = null)
        {
            var schoolInfo = await _context.SchoolInfos.FirstAsync();
            string MailServer = schoolInfo.EmailServer;
            int Port = schoolInfo.EmailPortNumber;
            string FromEmail = schoolInfo.FromEmail;
            string Password = schoolInfo.Password;
            var client = new SmtpClient(MailServer, Port)
            {
                Credentials = new NetworkCredential(FromEmail, Password),
                EnableSsl = true,
            };
            MailMessage mailMessage = new MailMessage
            {
                From = new MailAddress(FromEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHTML,

            };
            mailMessage.To.Add(toEmail);

            mailMessage.Headers.Add("Disposition-Notification-To", FromEmail);
            if (pdf != null)
            {
                Attachment pdfAttachment = new Attachment(new MemoryStream(pdf), pdfName ?? "pdf.pdf", "application/pdf");
                mailMessage.Attachments.Add(pdfAttachment);
            }
            //byte[] bytes = Convert.FromBase64String(schoolInfo.LogoBase64);
            //MemoryStream ms = new MemoryStream(bytes);
            //LinkedResource inlineLogo = new LinkedResource(ms, "image/png")
            //{
            //    ContentId = "MyImage",
            //    TransferEncoding = TransferEncoding.Base64
            //};
            //AlternateView htmlView = AlternateView.CreateAlternateViewFromString(body, null, "text/html");
            //htmlView.LinkedResources.Add(inlineLogo);
            //mailMessage.AlternateViews.Add(htmlView);
            return client.SendMailAsync(mailMessage);
        }
        

        private string Message(string contentHtml)
        {
            var schoolName = _context.SchoolInfos.First().SchoolName;
            string ConstantsMessage = @$"<div class='container' style='width: 100%;background-color: rgb(242, 242, 242);text-align: center;padding: 20px 0;margin: 0;'>
                <div class='message' style=' width: 300px;margin: 0 auto;'>
                    <table style='width:90%;margin: 0 auto;'>
                        <tr>
                            <td style='text-align:left;'>
                                <h3>{schoolName}</h3>
                            </td>
                            <td style='text-align:right;'>
                                <img style='width:40px;' src='https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRHv6N2GNpRENY0a68wIGbZC-_BNshBPI2xtVxfp5kMq5QLz9i1YECNXh1Klk8um8LXybQ&usqp=CAU' alt='Logo'/>
                            </td>
                        </tr>
                    </table>
                    <div class='content' style='background-color:rgb(255, 255, 255);padding:20px;'>
                    {contentHtml}
                    </div>
                    <footer>
                        <p>© 2024 NetWitcher. All rights reserved.</p>
                    </footer>
                </div>
            </div>";
            return ConstantsMessage;
        }
        public async Task<IActionResult> SendConfirmationEmail(ApplicationUser user, HttpContext httpContext)
        {
            var request = httpContext.Request;
            var domain = $"{request.Scheme}://{request.Host}";
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = $"{domain}/api/Account/ConfirmUser?ID={user.Id}&Token={token}";
            var content = $@"<div style='width:100%;text-align:left;'>
                            <h1 style='width:100%;text-align:center;'>Hello {user.UserName}</h1>
                             <p style='font-size:14px;'>Welcome to FekraHup!, Thank you For Confirming your Account,</p>
                             <p style='font-size:14px;'>The activation button is valid for <b> 7 Days</b>. Please activate the email before this period expires</p>
                            <p style='font-size:14px;'>To complete the confirmation, please click the confirm button</p><br><br/>
                            <div style='width:100%;text-align:center'> <a href='{confirmationLink}' style='text-decoration: none;color: white;padding: 10px 25px;border: none;border-radius: 4px;font-size: 20px;background-color: rgb(83, 136, 247);'>confirm</a>
                            <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div> </div>
                            ";
            try
            {
                await SendEmail(user.Email ?? "", "Please Confirm Your Email", Message(content), true);
                return new OkResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
                return new BadRequestObjectResult($"Error sending email: {ex.Message}");
            }
        }

        public async Task<IActionResult> SendContractEmail(int studentId, string pdfName)//
        {
            var student = await _studentRepo.GetById(studentId);
            var parent = await _userManager.FindByIdAsync(student.ParentID ?? "");
            if (student == null || parent == null)
            {
                return new BadRequestObjectResult("Something seems wrong. Please re-register your child or contact us");
            }
            var contracts = await _studentContract.GetAll();
            byte[] contract = contracts.Where(x => x.StudentID == studentId).Select(x => x.File).First();
            var content = @$"<div style='width:100%;text-align:left;'>
                        <h1 style='width:100%;text-align:center;'>Hello {parent.FirstName} {parent.LastName}</h1><hr></hr><br></br>
                         <p style='font-size:13px;'>Your son <b>{student.FirstName} {student.LastName}</b> has been registered successfully.</p>
                         <p style='font-size:13px;'>A copy of the contract has been sent to you. <br></br>
                        For more information contact us</p>
                        <div style='width:100%;text-align:center'><p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div>
                     </div>";
            try
            {
                await SendEmail(parent.Email ?? "", "Registration Confirmation", Message(content), true, contract, pdfName + ".pdf");
                return new OkResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
                return new BadRequestObjectResult($"Error sending email: {ex.Message}");
            }
        }

        public async Task SendToAdminNewParent(ApplicationUser user)
        {
            var AdminId = await _context.UserRoles.Where(x => x.RoleId == "1").Select(x => x.UserId).FirstOrDefaultAsync();
            var admin = await _userManager.Users.Where(x => x.Id == AdminId).FirstOrDefaultAsync();
            var content = @$"<div style='width:100%;text-align:left;'>
                        <h1 style='width:100%;text-align:center;'>Hello {admin.FirstName} {admin.LastName}</h1><hr></hr><br></br>
                        <p style='font-size:14px;'>Fekra Hub would like to tell you some new information about your school</p>
                         <p style='font-size:14px;'><b>A new family has been added .</b></p>
                         <p style='font-size:14px;'>family information :</p>
                           <ul>
                                <li>
                               Name : {user.FirstName} {user.LastName}
                                </li>
                                <li>
                                Email : {user.Email}
                                </li>
                                <li>
                                PhoneNumber : {user.PhoneNumber} / {user.EmergencyPhoneNumber}
                                </li>
                                <li>
                                Gender : {user.Gender}
                                </li>
                                <li>
                                Birthday : {user.Birthday}
                                </li>
                                <li>
                                Birthplace : {user.Birthplace}
                                </li>
                                <li>
                                Nationality : {user.Nationality}
                                </li>
                                <li>
                                Job : {user.Job}
                                </li>
                                <li>
                                City : {user.City}
                                </li>
                                <li>
                                StreetNr :{user.Street} {user.StreetNr} 
                                </li>
                                <li>
                                ZipCode : {user.ZipCode}
                                </li>
                                
                                
                            </ul>
                           <div style='width:100%;text-align:center'>
                        <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div>
                     </div>";
            await SendEmail(admin?.Email ?? "", "New User Registration", Message(content), true);
        }

        public async Task SendToAllNewEvent()
        {
            var users = await _userManager.Users.ToListAsync();
            var NotParentsId = await _context.UserRoles.Where(x => x.RoleId != "3").Select(x => x.UserId).ToListAsync();
            List<ApplicationUser> parent = users
                .Where(user => !NotParentsId.Contains(user.Id))
                .ToList();
            List<ApplicationUser> notParent = users
                .Where(user => NotParentsId.Contains(user.Id))
                .ToList(); 

            var students = await _studentRepo.GetAll();
            foreach (var user in parent)
            {
                var student = students.Where(x => x.ParentID == user.Id).ToList();
                if (student.Any())
                {
                    var childrenNames = "";
                    foreach (var child in student)
                    {
                        childrenNames += "<p 'font-size:14px;'>" + child.FirstName + " " + child.LastName + "</p>";
                    }

                    var content = @$"<div style='width:100%;text-align:left;'>
                            <h1 style='width:100%;text-align:center;'>Hello {user.FirstName} {user.LastName}</h1><hr></hr><br></br>
                            <p style='font-size:14px;'>Fekra Hub would like to tell you some new information about your children</p>
                            <p><b>Children Name :</b></p>
                            {childrenNames}
                             <p style='font-size:14px;'><b>A new event has been added .</b></p>
                            <p style='font-size:14px;'>For more information, please go to the events page on our official website or click the button to be directed to the page directly</p>
                           <br></br><div style='width:100%;text-align:center'> <a href='www.google.com' style='text-decoration: none;color: white;padding: 10px 25px;border: none;border-radius: 4px;font-size: 20px;background-color: rgb(83, 136, 247);'>event page</a>
                            <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div>
                         </div>";
                    await SendEmail(user.Email ?? "", "New Event", Message(content), true);
                }
            }
            foreach (var user in notParent)
            {
                var content = @$"<div style='width:100%;text-align:left;'>
                        <h1 style='width:100%;text-align:center;'>Hello {user.FirstName} {user.LastName}</h1><hr></hr><br></br>
                        <p style='font-size:14px;'>Fekra Hub would like to tell you some new information about your students</p>
                         <p style='font-size:14px;'><b>A new event has been added .</b></p>
                           <div style='width:100%;text-align:center'>
                        <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div>
                     </div>";
                await SendEmail(user.Email ?? "", "", Message(content), true);
            }

        }

        public async Task SendToParentsNewFiles(List<Student> students)
        {
            var parents = await _userManager.Users.ToListAsync();

            foreach (var student in students)
            {
                var parent = parents.Where(x => x.Id == student.ParentID).FirstOrDefault();
                if (parent == null)
                {
                    continue;
                }

                var content = @$"<div style='width:100%;text-align:left;'>
                        <h1 style='width:100%;text-align:center;'>Hello {parent.FirstName} {parent.LastName}</h1><hr></hr><br></br>
                        <p style='font-size:14px;'>Fekra Hub would like to tell you some new information about your children</p>
                         <p><b>Name : {student.FirstName} {student.LastName}</b></p>
                         <p style='font-size:14px;'><b>A new file has been added .</b></p>
                        <p style='font-size:14px;'>For more information, please go to the events page on our official website or click the button to be directed to the page directly</p>
                       <br></br><div style='width:100%;text-align:center'> <a href='www.google.com' style='text-decoration: none;color: white;padding: 10px 25px;border: none;border-radius: 4px;font-size: 20px;background-color: rgb(83, 136, 247);'>files page</a>
                        <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div>
                     </div>";
                    await SendEmail(parent.Email ?? "", "New Files", Message(content), true);
                }

        }

        public async Task SendToSecretaryNewReportsForStudents()
        {
            var SecretariesId = await _context.UserRoles
                            .Where(x => x.RoleId == "2")
                            .Select(x => x.UserId)
                            .ToListAsync();
            var Secretaries = await _userManager.Users
                         .Where(user => SecretariesId.Contains(user.Id))
                         .ToListAsync();
            foreach (var Secretary in Secretaries)
            {
                var content = @$"<div style='width:100%;text-align:left;'>
                        <h1 style='width:100%;text-align:center;'>Hello {Secretary.FirstName} {Secretary.LastName}</h1><hr></hr><br></br>
                        <p style='font-size:14px;'>Fekra Hub would like to tell you some new information about your students</p>
                         <p style='font-size:14px;'><b>New reports has been added .</b></p>
                           <div style='width:100%;text-align:center'>
                        <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div>
                     </div>";
                await SendEmail(Secretary.Email, "", Message(content), true, null, null);
            }

        }

        public async Task SendToParentsNewReportsForStudents(List<Student> students)
        {
            var parents = await _userManager.Users.ToListAsync();
             
            foreach (var student in students)
            {
                var parent = parents.Where(x => x.Id == student.ParentID).FirstOrDefault();
                if (parent == null)
                {
                    continue;
                }
                var content = @$"<div style='width:100%;text-align:left;'>
                        <h1 style='width:100%;text-align:center;'>Hello {parent.FirstName} {parent.LastName}</h1><hr></hr><br></br>
                        <p style='font-size:14px;'>Fekra Hub would like to tell you some new information about your children</p>
                        <p><b>Name : {student.FirstName} {student.LastName}</b></p>
                         <p style='font-size:14px;'><b>A new report has been added .</b></p>
                        <p style='font-size:14px;'>For more information, please go to the events page on our official website or click the button to be directed to the page directly</p>
                       <br></br><div style='width:100%;text-align:center'> <a href='www.google.com' style='text-decoration: none;color: white;padding: 10px 25px;border: none;border-radius: 4px;font-size: 20px;background-color: rgb(83, 136, 247);'>reports page</a>
                        <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div>
                     </div>";
                await SendEmail(parent.Email ?? "", "New Reports", Message(content), true);



            }
        }

        public async Task SendToTeacherReportsForStudentsNotAccepted(int studentId,string teacherId)
        {
            var student = await _studentRepo.GetById(studentId);
            if (await _studentRepo.IsTeacherIDExists(teacherId))
            {
                var teacher = await _userManager.FindByIdAsync(teacherId);
                var content = @$"<div style='width:100%;text-align:left;'>
                        <h1 style='width:100%;text-align:center;'>Hello {teacher.FirstName} {teacher.LastName}</h1><hr></hr><br></br>
                        <p style='font-size:14px;'>Fekra Hub would like to tell you some new information about your students</p>
                        <p><b>Student Name : {student.FirstName} {student.LastName}</b></p>
                         <p style='font-size:14px;'><b> report has been not accepteds .</b></p>
                        <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div>
                     </div>";
                await SendEmail(teacher.Email ?? "", "", Message(content), true);




            }
        }

        public async Task SendRestPassword(string email, string link)
        {
            var user = await _userManager.FindByEmailAsync(email);
            var schooleInfo = (await _schoolInfo.GetRelation()).First();
            var content = $@"<div style='width:100%;text-align:left;'>
                    <h1 style='width:100%;text-align:center;'>Hello {user.FirstName} {user.LastName}</h1>
                     <p style='font-size:14px;'>Welcome to {schooleInfo.SchoolName}!</p>
                    <p style='font-size:14px;'>To complete forget password, please click the button</p><br><br/>
                    <div style='width:100%;text-align:center'> <a href='{link}' style='text-decoration: none;color: white;padding: 10px 25px;border: none;border-radius: 4px;font-size: 20px;background-color: rgb(83, 136, 247);'>Click</a>
                    <p style='font-size:12px;margin-top:60px'>Thank you for your time. </p></div> </div>
                    ";
            await SendEmail(email ?? "", "Reset Password", Message(content), true);
        }
    }
}
