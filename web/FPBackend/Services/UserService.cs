using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ardalis.Result;
using FPBackend.Context;
using FPBackend.Models;
using FPBackend.Models.DTO;
using Microsoft.IdentityModel.Tokens;

namespace FPBackend.Services;

public interface IUserService {
    public Result<JwtResponseDTO> Authenticate(UserCredentialsDTO model);
    public IEnumerable<User> GetAllUsers();
    public Result<User> GetUserByEmail(string email);
    public Result<User> GetUserById(Guid id);
    public Task<Result<User>> CreateUserAsync(UserRegistrationDTO model, CancellationToken token);
}

public class UserService : IUserService {
    private readonly FPDataContext _context;
    private readonly IConfiguration _configuration;
    
    public UserService(FPDataContext context, IConfiguration config) {
        _configuration = config;
        _context = context;
    }

    public Result<JwtResponseDTO> Authenticate(UserCredentialsDTO model) {
        var user = GetUserByEmail(model.Email);
        if(!user.IsSuccess) return Result.Error(user.Errors.FirstOrDefault("An unknown error occured."));
        if (!VerifyPasswordHash(model.Password, user.Value.PasswordHash, user.Value.PasswordSalt)) return Result<JwtResponseDTO>.Error("Invalid Credentials");
        var token = GenerateJSONWebToken(user.Value);
        return Result.Success(new JwtResponseDTO(token.Item2, token.Item1));
    }

    public async Task<Result<User>> CreateUserAsync(UserRegistrationDTO model, CancellationToken cancellationToken = default) {
        if (!GetUserByEmail(model.EmailAddress.ToLower().Trim()).IsSuccess) {
            var passwdHashTuple = CreatePasswordHash(model.Password);
            var newUser = new User() {
                AccountCreationDate = DateTime.Now,
                Administrator = false,
                Email = model.EmailAddress.ToLower().Trim(),
                Steam64 = model.Steam64ID,
                PasswordSalt = passwdHashTuple.Item1,
                PasswordHash = passwdHashTuple.Item2
            };
            await _context.Users.AddAsync(newUser, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<User>.Success(newUser);
        }

        return Result<User>.Error("A user with this email address already exists");
    }

    public IEnumerable<User> GetAllUsers() => _context.Users;

    public Result<User> GetUserByEmail(string email) {
        email = email.ToLower().Trim();
        var result = _context.Users.ToList().FirstOrDefault(u => u.Email == email);
        return (result is null) ? Result<User>.NotFound("No user with this email was found.") : Result.Success(result);
    }

    public Result<User> GetUserById(Guid id) {
        var result = _context.Users.Find(id);
        return (result is null) ? Result<User>.NotFound("No user with this id was found.") : Result.Success(result);
    }
    
    //Helpers

    private (byte[], byte[]) CreatePasswordHash(string password) {
        using var hmac = new HMACSHA512();
        return (hmac.Key, hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }

    private (DateTime, string) GenerateJSONWebToken(User userInfo, int tokenLifetimeMinutes = 120) {    
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));    
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512Signature);
        var expiration = DateTime.Now.AddMinutes(tokenLifetimeMinutes);
            
        var token = new JwtSecurityToken(_configuration["Jwt:Issuer"],    
            _configuration["Jwt:Issuer"],    
            new [] {
                new Claim(JwtRegisteredClaimNames.Sub, userInfo.Email),
                new Claim(JwtRegisteredClaimNames.Email, userInfo.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("id", userInfo.Id.ToString())
            },    
            expires: expiration,    
            signingCredentials: credentials);    
    
        return (expiration, new JwtSecurityTokenHandler().WriteToken(token));    
    }  


    private bool VerifyPasswordHash(string password, byte[] hash, byte[] salt) {
        using var hmac = new HMACSHA512(salt);
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return computed.SequenceEqual(hash);
    }
}