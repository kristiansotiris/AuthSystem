using AuthSystem.Dto;
using AuthSystem.Models;
using AuthSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthSystem.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class UserControllers(UserService userService, TokenService tokenService) : ControllerBase
    {
        private readonly UserService _userServices = userService;
        private readonly TokenService _tokenServices = tokenService;


        [Authorize]
        [HttpGet("me")]
        public ActionResult GetCurrentUser()
        {
            var userClaims = User.Claims.ToDictionary(c => c.Type, c => c.Value);

            return Ok(new
            {
                Email = userClaims.GetValueOrDefault(ClaimTypes.Email),
                Name = userClaims.GetValueOrDefault(ClaimTypes.Name),
                GoogleId = userClaims.GetValueOrDefault(ClaimTypes.NameIdentifier)
            });
        }

        [AllowAnonymous]
        [HttpGet("login-google")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/google-response"
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }


        [AllowAnonymous]
        [HttpGet("google-response")]
        public async Task<ActionResult<LoginResponseDto>> GoogleResponse()
        {
            try
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                if (!result.Succeeded)
                    return BadRequest(new { Error = "Google Authentication Failed !" });

                var email = result.Principal.FindFirstValue(ClaimTypes.Email);
                var name = result.Principal.FindFirstValue(ClaimTypes.Name);
                var googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { Error = "Failed to retrieve email from Google!" });

                var googleDto = new GoogleLoginDto
                {
                    Email = email,
                    UserName = name,
                    GoogleId = googleId
                };

                var (user, message) = await _userServices.GoogleLoginAsync(googleDto);

                if (user == null) return BadRequest(new { Message = message });

                var token = _tokenServices.GenerateToken(user);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTime.UtcNow.AddDays(7)
                };

                Response.Cookies.Append("auth_user", token, cookieOptions);

                return Redirect("https://localhost:3000/dashboard");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }


        [HttpPost("signup")]
        public async Task<ActionResult<RegisterResponseDto>> SignUpAsync(RegisterUserDto register)
        {
            try
            {
                var newUser = await _userServices.RegisterAsync(register);
                if (newUser.User == null) return BadRequest(new { Error = newUser.Message });
                return CreatedAtAction(nameof(GetUsers), new {id = newUser.User.Id}, newUser);

            }
            catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("logout")]
        public async Task<ActionResult> LogOutAsync()
        {
            try
            {
                Response.Cookies.Delete("auth_user");

                return Ok(new { Message = "You Have Been Logged Out !" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("signin")]
        public async Task<ActionResult<LoginResponseDto>> SingInAsync(LogInDto login)
        {
            try
            {
                var user = await _userServices.LogInAsync(login);
                if (user == null) return Unauthorized($"{user} Invalid Cretiria");

                var token = _tokenServices.GenerateToken(user);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddDays(7)
                };

                Response.Cookies.Append("auth_user", token, cookieOptions);


                var response = new LoginResponseDto
                {

                    User = new UserResponseDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        UserName = user.UserName,
                        Role = user.Role,
                        Status = user.Status,
                        AccountStatus = user.AccountStatus,
                        CreatedAt = user.CreatedAt,
                    },

                    Message = "Succesfull Logged In !",
                };

                return Ok(response);

            }
            catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        [Authorize]
        [HttpPatch("update/{id}")]
        public async Task<ActionResult<UpdateUserResponse>> UpdateUserAsync(string id, [FromBody] UpdateUserDto updateUserDto)
        {
            try
            {
                var existingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var IsAdmin = User.IsInRole("Admin");

                if (existingUserId != id && !IsAdmin) return Forbid();

                var updatedUser = await _userServices.UpdateUser(id, updateUserDto);

                if (updatedUser == null) return NotFound("User not found !");

                return Ok(updatedUser);


            }catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<ActionResult<DeleteResponse>> DeleteUserAsync([FromQuery] string id)
        {
            try
            {
                return (await _userServices.DeleteAsync(id));
            }
            catch(ArgumentException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { ex.Message });
            }
        }


        [Authorize(Roles = "Admin")]
        [HttpPatch("ban")]
        public async Task<ActionResult<BanResponseDto>> BanUserAsync([FromQuery] string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return NotFound($"UserId required !");


                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserRoleStr = User.FindFirstValue(ClaimTypes.Role);

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return Unauthorized("User id not found in token!");


                if (string.IsNullOrWhiteSpace(currentUserRoleStr))
                    return Unauthorized("Role not found in token!");

                if (!Enum.TryParse<Roles>(currentUserRoleStr, out var currentUserRole))
                    return BadRequest("Invalid role in token!");

                var bannedUser = await _userServices.BanAsync(id, currentUserId, currentUserRole);
                return (bannedUser != null) ? Ok(bannedUser) : NotFound($"User with id: {id} not found !");


            }catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("unban")]
        public async Task<ActionResult<BanResponseDto>> UnBanUserAsync([FromQuery] string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return NotFound("User id not found !");

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserRoleStr = User.FindFirstValue(ClaimTypes.Role);

                if (string.IsNullOrWhiteSpace(currentUserId)) return Unauthorized("User admin id not found !");
                if (string.IsNullOrWhiteSpace(currentUserRoleStr)) return Unauthorized("User Admin Role Not found !");

                if (!Enum.TryParse<Roles>(currentUserRoleStr, out var currentUserRole)) return BadRequest("Invalid Role");

                var unban_user = await _userServices.UnBanAsync(id, currentUserId, currentUserRole);

                return (unban_user != null) ? Ok(unban_user) : NotFound($"User with id: {id} not found !");

            }
            catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("users")]
        public async Task<ActionResult<List<UserResponseDto>>> GetUsers([FromQuery]PaginationDto pagination)
        {
            try
            {
                var users = await _userServices.GetUsersListAsync(pagination);
                return (users != null) ? Ok(users) : NotFound("Users Not found Or List is Empty !");

            }catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("search")]
        public async Task<ActionResult<PaginationResponse<UserResponseDto>>> SearchUsersAsync([FromQuery] SearchDto search, [FromQuery] PaginationDto pagination)
        {
            try
            {
                var users = await _userServices.SearchAsync(search, pagination);
                return (users != null) ? Ok(users) : NotFound("Nothing Found !");

            } catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("search_account_status")]
        public async Task<ActionResult<PaginationResponse<UserResponseDto>>> SearchUserByAccountStatus([FromQuery] SearchDto search, [FromQuery] PaginationDto pagination)
        {
            try
            {
                var users = await _userServices.GetAccountStatus(pagination,search);
                return (users != null) ? Ok(users) : NotFound("Nothing Found !");

            }catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("search_status")]

        public async Task<ActionResult<List<UserResponseDto>>> SearchUsersByStatus([FromQuery] SearchDto search, [FromQuery] PaginationDto pagination)
        {
            try
            {
                var users = await _userServices.GetUserStatusAsync(search, pagination);
                return (users != null) ? Ok(users) : NotFound("Nothing Found !");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("send_email_verification")]
        public async Task<ActionResult<VerificationResponse>> SendVerificationCodeAsync([FromBody] EmailDto email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email.Email)) return BadRequest(new {Error = "Email is Required !" });
                var result = await _userServices.SendVerificationCodeAsync(email);
                return Ok(result);

            }catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("verify_email")]
        public async Task<ActionResult<VerificationResponse>> VerifyUserEmail([FromQuery] string token, [FromBody] VerificationDto verification)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(verification.Code)) return BadRequest(new { Error = "Code is required !" });

                var result = await _userServices.VerifyEmailAsync(token,verification);
                return Ok(result);

            }catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        [AllowAnonymous]
        [HttpPost("forgot_password")]
        public async Task<ActionResult<VerificationResponse>> ForgotUserPassword([FromBody] EmailDto email)
        {
            try
            {

                if (string.IsNullOrWhiteSpace(email.Email)) return BadRequest("Email is Required !");
                var user = await _userServices.ForgotPasswordAsync(email);

                return (user != null) ? Ok(user) : NotFound($"We couldn't find any account associated with the email '{email.Email}'. Please check the address and try again.");

            }
            catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }

        }

        [HttpPost("reset_password")]
        public async Task<ActionResult<VerificationResponse>> ResetUserPassword([FromQuery] string token, [FromBody] ResetPassword password)
        {
            try
            {
                var user = await _userServices.ChangePasswordAsync(token, password);

                return StatusCode(user.StatusCode, user);

            }
            catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }



        [Authorize(Roles = "Admin")]
        [HttpGet("user")]
        public async Task<ActionResult<UserResponseDto>> FetchUserById([FromQuery] string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return Conflict("User id is required !");

                var user = await _userServices.GetUserByIdAsync(id);

                return (user != null) ? Ok(user) : NotFound($"User with id: {id} not found !");

            }
            catch(ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

    }

}
