﻿using KMG.Repository.Repositories;
using KMG.Repository.Base;
using Microsoft.EntityFrameworkCore;
using KMG.Repository;
using KMG.Repository.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using System.Text.RegularExpressions;

namespace KMS.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {

        private readonly AddressRepository _addressRepository;
        private readonly UserRepository _userRepository;
        private readonly string _secretKey;

        public UserController(UnitOfWork unitOfWork)
        {
            _userRepository = new UserRepository(unitOfWork.UserRepository._context);
            _addressRepository = new AddressRepository(unitOfWork.AddressRepository._context);

            _secretKey = "xinchaocacbanminhlasang1234567890";
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>>
            GetUser()
        {
            var UserList = await _userRepository.GetAllAsync();
            Console.WriteLine($"Number of User retrieved: {UserList.Count}");
            return Ok(UserList);

        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }


            return Ok(user);
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Login loginModel)
        {
            if (string.IsNullOrEmpty(loginModel.UserName) || string.IsNullOrEmpty(loginModel.Password))
            {
                return BadRequest("Username and Password are required.");
            }

            var user = await _userRepository.AuthenticateAsync(loginModel.UserName, loginModel.Password);

            if (user == null)
            {
                return Unauthorized("Invalid username or password.");
            }
            if (user.Status == "locked")
            {
                return Unauthorized("This account has been locked.");
            }
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Role, user.Role ?? "customer"),
                    new Claim("UserId", user.UserId.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);


            return Ok(new
            {
                Message = "Login successful",
                Token = tokenString,
                User = user
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Register registerModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = await _userRepository.RegisterAsync(registerModel.UserName, registerModel.Password, registerModel.Email);

            if (user == null)
            {
                return BadRequest("Username or Email already exists.");
            }

            return Ok(new
            {
                Message = "Registration successful",
                User = user
            });
        }
        [HttpPost("registerForStaff")]
        public async Task<IActionResult> RegisterStaff([FromBody] Register registerModel)
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }


            var user = await _userRepository.RegisterStaffAsync(registerModel.UserName, registerModel.Password, registerModel.Email);

            if (user == null)
            {
                return BadRequest("Username or Email already exists.");
            }

            return Ok(new
            {
                Message = "Registration successful",
                User = user
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {

            var result = await _userRepository.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound(new { message = "User not found." });
            }
            result.Status = "locked";
            await _userRepository.SaveAsync();
            return Ok(new { message = "User is locked" });

        }
        [HttpPut("restore/{id}")]
        public async Task<IActionResult> RestoreUser(int id)
        {

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }
            if (user.Status != "locked")
            {
                return BadRequest("User is already active men .");
            }
            user.Status = "active";
            await _userRepository.SaveAsync();

            return Ok(new { message = "User is now active." });
        }
        [HttpPost("ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePassword model)
        {
            var curhashedPassword = HashPassword.HashPasswordToSha256(model.CurrentPassword);
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var users = await _userRepository.GetAll().ToListAsync();
            var user = users.FirstOrDefault(u => u.UserName == model.UserName);

            if (user == null)
            {
                return NotFound("User not found.");
            }
            if (user.Password != curhashedPassword)
            {
                return BadRequest("Current password is incorrect.");
            }
            var newhashedPassword = HashPassword.HashPasswordToSha256(model.NewPassword);
            user.Password = newhashedPassword;
            await _userRepository.SaveAsync();

            return Ok(new { Message = "Password changed successfully." });
        }

        [HttpPut("updateProfile{id}")]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateProfile model)
        {


            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (!string.IsNullOrEmpty(model.UserName))
            {
                user.UserName = model.UserName;
            }

            if (!string.IsNullOrEmpty(model.Email))
            {
                user.Email = model.Email;
            }

            if (!string.IsNullOrEmpty(model.PhoneNumber))
            {
                user.PhoneNumber = model.PhoneNumber;
            }
            if (!Regex.IsMatch(model.PhoneNumber, @"^\d{10}$"))
            {
                return BadRequest("Phone number must be 10 digits.");
            }
            if (!string.IsNullOrEmpty(model.Address))
            {
                var existingAddresses = await _addressRepository.GetAll()
             .Where(a => a.UserID == id).ToListAsync();

                foreach (var addr in existingAddresses)
                {
                    addr.IsDefault = false;
                    await _addressRepository.UpdateAsync(addr);
                }


                var newAddress = new Address
                {
                    UserID = id,
                    address = model.Address,
                    AddressType = "home",
                    IsDefault = true
                };

                await _addressRepository.CreateAsync(newAddress);
                user.Address = model.Address;
            }

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveAsync();
            await _addressRepository.SaveAsync();

            return Ok(new { Message = "Profile updated successfully.", User = user });
        }
        [HttpGet("getAddressesByUserId/{userId}")]
        public async Task<IActionResult> GetAddressesByUserId(int userId)
        {

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }


            var addresses = await _addressRepository.GetAll()
                .Where(a => a.UserID == userId)
                .Select(a => new
                {
                    AddressID = a.AddressID,
                    Address = a.address,
                    AddressType = a.AddressType,
                    IsDefault = a.IsDefault
                }).ToListAsync();


            if (addresses == null || !addresses.Any())
            {
                return NotFound("No addresses found for this user.");
            }

            return Ok(addresses);
        }
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] string idToken)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            var googleTokenValidationUrl = "https://www.googleapis.com/oauth2/v3/tokeninfo?id_token=" + idToken;

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(googleTokenValidationUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest("Invalid Google token.");
                }

                var googleResponse = await response.Content.ReadFromJsonAsync<GoogleTokenInfo>();
                if (googleResponse == null || string.IsNullOrEmpty(googleResponse.Email))
                {
                    return BadRequest("Google token validation failed.");
                }
                var registeredUser = await _userRepository.RegisterGoogle(googleResponse.Name, googleResponse.Email);

                if (registeredUser == null)
                {
                    registeredUser = await _userRepository.GetAll().FirstOrDefaultAsync(u => u.Email == googleResponse.Email);
                }


                var claims = new List<Claim>
        {
                new Claim(ClaimTypes.Name, googleResponse.Name),
                new Claim(ClaimTypes.Email, googleResponse.Email),
                new Claim("UserId", registeredUser.UserId.ToString()),
                new Claim(ClaimTypes.Role, registeredUser.Role)
        };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: "yourapp",
                    audience: "yourapp",
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(30),
                    signingCredentials: creds);

                return Ok(new
                {
                    Token = new JwtSecurityTokenHandler().WriteToken(token),
                    User = registeredUser
                });
            }
        }

    }
}
