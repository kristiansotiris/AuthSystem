using AuthSystem.Dto;
using AuthSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AuthSystem.Services
{
    public class UserService
    {
        private readonly IMongoCollection<User> _userCollection;

        public UserService(IOptions<DatabaseSettings> options, TokenService tokenService)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.Value.ConnectionString)) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.Value.DatabaseName)) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.Value.Collection)) throw new ArgumentNullException(nameof(options));

            var mongoClient = new MongoClient(options.Value.ConnectionString);
            var database = mongoClient.GetDatabase(options.Value.DatabaseName);
            _userCollection = database.GetCollection<User>(options.Value.Collection);

        }


        public async Task<PaginationResponse<UserResponseDto>> GetUsersListAsync(PaginationDto pagination)
        {
            if (pagination.Page < 1) pagination.Page = 1;
            if (pagination.PageSize < 1) pagination.PageSize = 10;
            if (pagination.PageSize > 100) pagination.PageSize = 100;

            var totalCount = await _userCollection.CountDocumentsAsync(_ => true);

            var users = await _userCollection.Find(_ => true).Skip((pagination.Page - 1) * pagination.PageSize).Limit(pagination.PageSize).ToListAsync();

            var usersDto = users.Select(u => new UserResponseDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                Role = u.Role,
                Status = u.Status,
                AccountStatus = u.AccountStatus,
                CreatedAt = u.CreatedAt,
            }).ToList();


            return new PaginationResponse<UserResponseDto>
            {
                Data = usersDto,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalCount = (int)totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pagination.PageSize),
                HasNextPage = pagination.Page < Math.Ceiling((double)totalCount / pagination.PageSize),
                HasPreviousPage = pagination.Page > 1
            };

        }


        public async Task<(User? user, string Message)> GoogleLoginAsync(GoogleLoginDto loginDto)
        {
            if (string.IsNullOrWhiteSpace(loginDto.Email)) return (null, Message: "Email Is Required !");

            var user = await GetUserByEmail(loginDto.Email);

            if (user == null)
            {
                var newUser = new User
                {
                    Email = loginDto.Email,
                    UserName = loginDto.UserName ?? loginDto.Email.Split("@")[0],
                    EmailVerified = true,
                    GoogleId = loginDto.GoogleId,
                    PasswordHash = null,
                    Status = Status.Online
                };

                await _userCollection.InsertOneAsync(newUser);
                user = newUser;
            }

            return (user, "Succesfully Logged In With Google Account !");
        }

        public async Task<RegisterResponseDto> RegisterAsync(RegisterUserDto register)
        {
            if (string.IsNullOrEmpty(register.UserName)) return new RegisterResponseDto { User = default!, Message = "UserName is Required !" };
            if (string.IsNullOrEmpty(register.Email)) return new RegisterResponseDto { User = default!, Message = "Email is Required !" };
            if (string.IsNullOrEmpty(register.PasswordHash)) return new RegisterResponseDto { User = default!, Message = "Password is Required !" };

            if (await IsSameEmail(register.Email))
            {
                return new RegisterResponseDto
                {
                    User = default!,
                    Message = "Email Already Exists"
                };
            }

            var newUser = new User
            {
                UserName = register.UserName,
                Email = register.Email,
                PasswordHash = HashedPassword(register.PasswordHash)
            };

            await _userCollection.InsertOneAsync(newUser);

            return new RegisterResponseDto
            {
                User = new UserResponseDto
                {
                    Id = newUser.Id,
                    UserName = newUser.UserName,
                    Email = newUser.Email,
                    Role = newUser.Role,
                    Status = newUser.Status,
                    AccountStatus = newUser.AccountStatus,
                    CreatedAt = newUser.CreatedAt,
                    UpdatedAt = default!
                },

                Message = "Succesfully Sing Up !"
            };

        }


        public async Task<User> LogInAsync(LogInDto login)
        {

            if (login == null) return default!;
            if (string.IsNullOrWhiteSpace(login.Email)) return default!;
            if (string.IsNullOrWhiteSpace(login.Password)) return default!;

            var user = await GetUserByEmail(login.Email);

            if (user == null) return default!;

            if (!VerifyPassword(login.Password, user.PasswordHash)) return default!;

            return user;
        }


        public async Task<UpdateUserResponse> UpdateUser(string id, UpdateUserDto updateDto )
        {
            if (string.IsNullOrWhiteSpace(id)) return default!;

            var user = await _userCollection.Find(u => u.Id == id).FirstOrDefaultAsync();

            if (user == null) return new UpdateUserResponse { Message = "User not found !" };

            if(!string.IsNullOrEmpty(updateDto.UserName))
            {
                var existingUserName = await _userCollection.Find(u => u.UserName == updateDto.UserName && u.Id != user.Id).FirstOrDefaultAsync();
                if (existingUserName != null) return new UpdateUserResponse { Message = "UserName is already taken" };

                user.UserName = updateDto.UserName;
            }

            if(!string.IsNullOrWhiteSpace(updateDto.Email))
            {
                var existingEmail = await _userCollection.Find(u => u.Email == updateDto.Email && u.Id != user.Id).FirstOrDefaultAsync();

                if (existingEmail != null) return new UpdateUserResponse { Message = "Email already exists !" };

                user.Email = updateDto.Email;
                user.EmailVerified = false;
            }

            if(Enum.IsDefined<Status>(updateDto.Status) && user.Status != updateDto.Status)
            {
                user.Status = updateDto.Status;
            }

            user.UpdatedAt = DateTime.UtcNow;

            await _userCollection.ReplaceOneAsync(u => u.Id == user.Id, user);

            return new UpdateUserResponse
            {
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    Status = user.Status,
                    AccountStatus = user.AccountStatus,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                },
                Message = "Your Informations have been Updated !"
            };

        }

        public async Task<DeleteResponse> DeleteAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return new DeleteResponse { Message = "Id is required !"};

            var user = await GetUserByIdAsync(id);

            if (user == null) return new DeleteResponse { Message = " User Not found !"};

            if(user.Role == Roles.Admin && user.Id == id)
            {

                return new DeleteResponse { Message = "You Cannot Delete your Account" };
            }

            var admincount = await _userCollection.CountDocumentsAsync(u => u.Role == Roles.Admin);

            if (admincount <= 1) return new DeleteResponse { Message = "Cannot delete the last admin account" };

            await _userCollection.DeleteOneAsync(id);

            return new DeleteResponse
            {
                Message = "Account is being Succesfully Deleted !"
            };

        }


        public async Task<BanResponseDto> BanAsync(string id, string currentUserId, Roles currentUserRole)
        {
            if (string.IsNullOrWhiteSpace(id)) return new BanResponseDto { User = default!, Message = "Id is required !" }; ;

            if (string.IsNullOrWhiteSpace(currentUserId)) return new BanResponseDto { User = default!, Message = "User id is required !" };

            if (!Enum.IsDefined<Roles>(currentUserRole)) return new BanResponseDto { User = default!, Message = "This Role is not defined to our list !" };

            if (id == currentUserId) return new BanResponseDto { User = default!, Message = "You Can't ban your self !" };

            var user = await _userCollection.Find(u => u.Id == id).FirstOrDefaultAsync();

            if (user == null) return default!;

            if (user.Role >= currentUserRole) return new BanResponseDto { User = default!, Message = "You Can't ban higher role account !" };

            user.AccountStatus = AccountStatus.Banned;
            await _userCollection.ReplaceOneAsync(u => u.Id == id, user);

            return new BanResponseDto
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
                    UpdatedAt = DateTime.UtcNow
                },
                Message = "User is being Banned !"
            };
        }

        public async Task<BanResponseDto> UnBanAsync(string id, string currentUserId, Roles currentUserRole)
        {
            if (string.IsNullOrWhiteSpace(id)) return new BanResponseDto { User = default!, Message = "User id is required !" };
            if (string.IsNullOrWhiteSpace(currentUserId)) return new BanResponseDto { User = default!, Message = "User Admin Id is Not Found !" };
            if (!Enum.IsDefined<Roles>(currentUserRole)) return new BanResponseDto { User = default!, Message = "Role is not defined to Roles List !" };

            var user = await _userCollection.Find(u => u.Id == id).FirstOrDefaultAsync();

            if (user == null) return new BanResponseDto { User = default!, Message = $"User with id: {id}" };

            if (user.Role >= currentUserRole) return new BanResponseDto { Message = "You Can't unban this account due to his role !" };

            if (user.Id == currentUserId) return new BanResponseDto { Message = "You can't unban your self !" };

            if (user.AccountStatus != AccountStatus.Banned) return new BanResponseDto { Message = "User is not banned !" };


            user.AccountStatus = AccountStatus.Active;
            await _userCollection.FindOneAndReplaceAsync(u => u.Id == id, user);

            return new BanResponseDto
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
                    UpdatedAt = DateTime.UtcNow
                },
                Message = "User is being Unbanned !"
            };
        }


        public async Task<VerificationResponse> SendVerificationCodeAsync(EmailDto email)
        {
            if (string.IsNullOrWhiteSpace(email.Email)) return new VerificationResponse { Message = "Email is Required !" };

            var existingUser = await GetUserByEmail(email.Email);

            if (existingUser == null) return new VerificationResponse { Message = $"User with email: {email.Email} doesnt Exist Please try to Sing Up First" };

            if (existingUser.EmailVerified) return new VerificationResponse { Message = "User is already Verified !" };


            if (existingUser.VerificationAttempts >= 2 && existingUser.LastVerificationSentAt > DateTime.UtcNow.AddMinutes(-15))
            {
                var timeleft = DateTime.UtcNow - existingUser.LastVerificationSentAt.Value;
                var minutesLeft = 15 - (int)timeleft.TotalMinutes;

                return new VerificationResponse { Message = $"Maximum Attempts Reached, Please Try Again in {minutesLeft} minutes " };
            }

            if(existingUser.LastVerificationSentAt == null || existingUser.LastVerificationSentAt < DateTime.UtcNow.AddMinutes(-15))
            {
                existingUser.VerificationAttempts = 0;
            }

            var code = GenerateVerificationCode();
            var token = GenerateVerificationToken();

            existingUser.VerificationAttempts++;
            existingUser.LastVerificationSentAt = DateTime.UtcNow;

            existingUser.VerificationCode = code;
            existingUser.VerificationToken = token;
            existingUser.VerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
            existingUser.UpdatedAt = DateTime.UtcNow;

            await _userCollection.ReplaceOneAsync(u => u.Id == existingUser.Id, existingUser);


            return new VerificationResponse
            {
                VerificationCode = code,
                VerificationToken = token,
                Url = $"{{URL3}}/api/auth/verify_email?token={token}",
                Message = $"Verification Send !"
            };
        }


        public async Task<VerificationResponse> VerifyEmailAsync(string token, VerificationDto verification)
        {
            if (string.IsNullOrWhiteSpace(verification.Code)) return new VerificationResponse {Message = "Code is not found !" };
            if (string.IsNullOrWhiteSpace(token)) return new VerificationResponse { Message = "Token not found !" };

            var user = await _userCollection.Find(u => u.VerificationToken == token).FirstOrDefaultAsync();

            if (user == null || user.VerificationToken == null) return new VerificationResponse { Message = "Invalid verification token" };

            if (user.EmailVerified) return new VerificationResponse { Message = "User is already Verified !" };

            if(user.VerificationCodeExpiresAt == null || user.VerificationCodeExpiresAt < DateTime.UtcNow)
            {
                return new VerificationResponse { Message = "Verification Code expired !" };
            }

            if(user.VerificationCode != verification.Code)
            {
                return new VerificationResponse { Message = "Verification codes are not the same !" };
            }

            user.EmailVerified = true;
            user.VerificationToken = null;
            user.VerificationCode = null;
            user.VerificationCodeExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userCollection.ReplaceOneAsync(u => u.Id == user.Id, user);

            return new VerificationResponse
            {
                Message = "You Haved Been Succesfully Verified"
            };

        }


        public async Task<VerificationResponse> ForgotPasswordAsync(EmailDto email)
        {
            if (string.IsNullOrWhiteSpace(email.Email))
                return new VerificationResponse
                {
                    Message = "Email is required!",
                    StatusCode = 400 // Bad Request
                };

            var user = await GetUserByEmail(email.Email);

            // 1. Πρώτα ελέγχεις αν υπάρχει ο user
            if (user == null)
                return new VerificationResponse
                {
                    Message = $"We couldn't find any account associated with the email '{email.Email}'. Please check the address and try again.",
                    StatusCode = 404 // Not Found
                };

            // 2. Μετά ελέγχεις αν έχει verified το email
            if (!user.EmailVerified)
                return new VerificationResponse
                {
                    Message = "You have to verify your email first!",
                    StatusCode = 403 // Forbidden
                };

            // 3. Rate limiting check
            if (user.PasswordResetAttempts >= 2 &&
                user.LastPasswordResetSentAt.HasValue &&
                user.LastPasswordResetSentAt.Value > DateTime.UtcNow.AddMinutes(-15))
            {
                var timeLeft = DateTime.UtcNow - user.LastPasswordResetSentAt.Value;
                var minutesLeft = 15 - (int)timeLeft.TotalMinutes;

                return new VerificationResponse
                {
                    Message = $"Too many attempts. Please try again in {minutesLeft} minute(s).",
                    StatusCode = 429 // Too Many Requests
                };
            }

            // 4. Reset attempts αν το token έχει λήξει
            if (user.LastPasswordResetSentAt == null || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
            {
                user.PasswordResetAttempts = 0;
            }

            var token = GenerateVerificationToken();

            user.PasswordResetAttempts++;
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
            user.LastPasswordResetSentAt = DateTime.UtcNow;
            user.PasswordResetToken = token;
            user.UpdatedAt = DateTime.UtcNow;

            await _userCollection.ReplaceOneAsync(u => u.Id == user.Id, user);

            // TODO: Send email with reset link
            // await _emailService.SendPasswordResetEmail(user.Email, token);

            return new VerificationResponse
            {
                Message = "Password reset link sent successfully!",
                Url = $"{{URL3}}/api/auth/reset_password?token={token}",
                StatusCode = 200
            };
        }


        public async Task<VerificationResponse> ChangePasswordAsync(string token, ResetPassword password)
        {
            if (string.IsNullOrWhiteSpace(token)) return new VerificationResponse { Message = "Token is required !" };
            if (string.IsNullOrWhiteSpace(password.Password)) return new VerificationResponse { Message = "Password is required !" };

            var validationPassword = ValidatePassword(password.Password);

            if (!string.IsNullOrEmpty(validationPassword)) return new VerificationResponse { Message = validationPassword };

            var user = await _userCollection.Find(u => u.PasswordResetToken == token).FirstOrDefaultAsync();

            if (user == null) return new VerificationResponse { Message = "The token you provided is invalid or has expired. Please request a new one and try again." };

            if (user.PasswordResetTokenExpiresAt == null || user.PasswordResetTokenExpiresAt < DateTime.UtcNow) return new VerificationResponse { Message = "Token Has Been Expired " };

            if (password.Password != password.ConfirmPassword) return new VerificationResponse { Message = "Passwords Do not Match !" };

            var hashedPassword = HashedPassword(password.Password);

            user.PasswordResetAttempts = 0;
            user.PasswordResetToken = null;
            user.LastPasswordResetSentAt = null;
            user.PasswordResetTokenExpiresAt = null;
            user.PasswordChangedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.PasswordHash = hashedPassword;

            await _userCollection.ReplaceOneAsync(u => u.Id == user.Id, user);

            return new VerificationResponse
            {
                Message = "Your Password has been Reseted !"
            };
        }




        public async Task<PaginationResponse<UserResponseDto>> SearchAsync(SearchDto search, PaginationDto pagination)
        {
            if (pagination.Page < 1) pagination.Page = 1;
            if (pagination.PageSize < 1) pagination.PageSize = 10;
            if (pagination.PageSize > 100) pagination.PageSize = 100;

            var filterbuilder = Builders<User>.Filter;
            var filter = filterbuilder.Empty;


            if (!string.IsNullOrWhiteSpace(search.UserName))
            {
                var usernamefilter = filterbuilder.Regex(u => u.UserName, new MongoDB.Bson.BsonRegularExpression(search.UserName, "i"));
                filter &= usernamefilter;
            }

            if (!string.IsNullOrWhiteSpace(search.Email))
            {
                var emailfilter = filterbuilder.Regex(u => u.Email, new MongoDB.Bson.BsonRegularExpression(search.Email, "i"));
                filter &= emailfilter;
            }

            if (Enum.IsDefined(search.Role))
            {
                var roleFilter = filterbuilder.Eq(u => u.Role, search.Role);
                filter &= roleFilter;
            }

            var totalCounts = await _userCollection.CountDocumentsAsync(filter);

            var user = await _userCollection.Find(filter).ToListAsync();

            var userDto = user.Select(u => new UserResponseDto
            {
                Id = u.Id,
                Email = u.Email,
                UserName = u.UserName,
                Role = u.Role,
                Status = u.Status,
                AccountStatus = u.AccountStatus,
                CreatedAt = u.CreatedAt

            }).ToList();

            return new PaginationResponse<UserResponseDto>
            {
                Data = userDto,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalCount = (int)totalCounts,
                TotalPages = (int)Math.Ceiling((double)totalCounts / pagination.PageSize),
                HasNextPage = pagination.Page < (int)Math.Ceiling((double)totalCounts / pagination.PageSize),
                HasPreviousPage = pagination.Page > 1

            };

        }


        public async Task<PaginationResponse<UserResponseDto>> GetAccountStatus(PaginationDto pagination,SearchDto search)
        {

            if (pagination.Page < 1)  pagination.Page = 1;
            if (pagination.PageSize < 1)  pagination.PageSize = 10;
            if (pagination.PageSize > 100) pagination.PageSize = 100;



            var filterBuilder = Builders<User>.Filter;
            var filter = filterBuilder.Empty;


            if (Enum.IsDefined<AccountStatus>(search.AccountStatus))
            {
                var accountStatus = filterBuilder.Eq(u => u.AccountStatus, search.AccountStatus);
                filter &= accountStatus;
            }

            var totalCount = await _userCollection.CountDocumentsAsync(filter);

            var user = await _userCollection.Find(filter).Skip((pagination.Page - 1) * pagination.PageSize).Limit(pagination.PageSize).ToListAsync();


            var userDto = user.Select(u => new UserResponseDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                Role = u.Role,
                Status = u.Status,
                AccountStatus = u.AccountStatus,
                CreatedAt = u.CreatedAt
            }).ToList();


            return new PaginationResponse<UserResponseDto>
            {
                Data = userDto,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalCount = (int)totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pagination.PageSize),
                HasNextPage = pagination.Page < (int)Math.Ceiling((double)totalCount / pagination.PageSize),
                HasPreviousPage = pagination.Page > 1
            };

        }


        public async Task<PaginationResponse<UserResponseDto>> GetUserStatusAsync(SearchDto search, PaginationDto pagination)
        {

            if (pagination.Page < 1) pagination.Page = 1;
            if (pagination.PageSize < 1) pagination.PageSize = 10;
            if (pagination.PageSize > 100) pagination.PageSize = 100;

            var filterbuilder = Builders<User>.Filter;
            var filter = filterbuilder.Empty;


            if (Enum.IsDefined<Status>(search.Status))
            {
                var userStatus = filterbuilder.Eq(u => u.Status, search.Status);
                filter &= userStatus;
            }

            var totalCount = await _userCollection.CountDocumentsAsync(filter);

            var users = await _userCollection.Find(filter).Skip((pagination.Page - 1) * pagination.PageSize).Limit(pagination.PageSize).ToListAsync();


            var usersDto = users.Select(u => new UserResponseDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                Role = u.Role,
                Status = u.Status,
                AccountStatus = u.AccountStatus,
                CreatedAt = u.CreatedAt

            }).ToList();

            return new PaginationResponse<UserResponseDto>
            {
                Data = usersDto,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalCount = (int)totalCount,
                TotalPages = (int)Math.Ceiling((double) totalCount / pagination.PageSize),
                HasNextPage = pagination.Page > (int)Math.Ceiling((double)totalCount / pagination.PageSize),
                HasPreviousPage = pagination.Page < 1

            };

        }

        public async Task<UserResponseDto?> GetUserByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var user = await _userCollection.Find(u => u.Id == id).FirstOrDefaultAsync();

            if (user == null) return null;

            return new UserResponseDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                AccountStatus = user.AccountStatus,
                CreatedAt = user.CreatedAt,
                UpdatedAt = default!
            };

        }


        private string ValidatePassword(string password)
        {
            if(password.Length < 8)
            {
                return "Password must be at least 8 characters long!";
            }

            if (!password.Any(char.IsUpper))
            {
                return "Password must containe at least one Upper Case Character!";
            }

            if (!password.Any(char.IsDigit))
            {
                return "Password must containe one digital number!";
            }

            if (!password.Any(chr => !char.IsLetterOrDigit(chr)))
            {
                return "Password must containe a special character!";
            }

            return string.Empty;
        }

        private async Task<bool> IsSameEmail(string email)
        {
            return await _userCollection.Find(u => u.Email == email).AnyAsync();
        }

        private async Task<User> GetUserByEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return default!;

            var user = await _userCollection.Find(u => u.Email == email).FirstOrDefaultAsync();

            if (user == null) return default!;

            return user;
        }

        private string GenerateVerificationToken()
        {
            var randomBytes = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        private string HashedPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private string GenerateVerificationCode()
        {
            return Random.Shared.Next(100000, 999999).ToString();
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}
