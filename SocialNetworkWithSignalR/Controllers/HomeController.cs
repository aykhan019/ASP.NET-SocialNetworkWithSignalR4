using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialNetworkWithSignalR.Entities;
using SocialNetworkWithSignalR.Models;
using System.Diagnostics;

namespace SocialNetworkWithSignalR.Controllers
{
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private UserManager<CustomIdentityUser> _userManager;
        private IHttpContextAccessor httpContextAccessor;
        private CustomIdentityDbContext _dbContext;

        public HomeController(UserManager<CustomIdentityUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            CustomIdentityDbContext dbContext)
        {
            _userManager = userManager;
            this.httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            ViewBag.User = user;
            return View();
        }

        public async Task<IActionResult> SendFollow(string id)
        {
            var sender = await _userManager.GetUserAsync(HttpContext.User);

            var receiverUser = _userManager.Users.FirstOrDefault(u => u.Id == id);
            if (receiverUser != null)
            {
                receiverUser.FriendRequests.Add(new FriendRequest
                {
                    Content = $"{sender.UserName} send friend request at {DateTime.Now.ToLongDateString()}",
                    SenderId = sender.Id,
                    CustomIdentityUser = sender,
                    ReceiverId = id,
                    Status = "Request"
                });

                await _userManager.UpdateAsync(receiverUser);


            }
            return Ok();
        }

        public async Task<IActionResult> GetMyFriends()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var myFriends=await _dbContext.Friends
                .Include(nameof(Friend.YourFriend)).ToListAsync();
            var result = myFriends.Where(f => f.OwnId == user.Id);

            return Ok(result);
        }

        public async Task<IActionResult> GetAllUsers()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var users = await _dbContext.Users.Include(nameof(CustomIdentityUser.FriendRequests))
                .Where(u => u.Id != user.Id)
                .OrderByDescending(x => x.IsOnline)
                .ToListAsync();
            var allRequests =await _dbContext.FriendRequests.ToListAsync();
            var myrequests =  allRequests.Where(f => f.SenderId == user.Id);

            var myfriends=await _dbContext.Friends.Where(f=>f.OwnId==user.Id || f.YourFriendId==user.Id).ToListAsync();

            foreach (var item in users)
            {
                var request = myrequests.FirstOrDefault(r => r.ReceiverId == item.Id && r.Status=="Request");
                if (request != null)
                {
                    item.HasRequestPending = true;
                }
                if (myfriends.Count()!=0)
                {
                    var friend =myfriends.FirstOrDefault(f => f.OwnId == item.Id || f.YourFriendId == item.Id);
                    if (friend != null)
                    {
                        item.IsFriend = true;
                    }
                }
            }

            return Ok(users);
        }

        public async Task<IActionResult> GetAllRequests()
        {
            var current = await _userManager.GetUserAsync(HttpContext.User);
            var users = _dbContext.Users.Include(nameof(CustomIdentityUser.FriendRequests));
            var user = users.FirstOrDefault(u => u.Id == current.Id);
            var items = user.FriendRequests.Where(r => r.ReceiverId == user.Id);
            return Ok(items);
        }


        public async Task<IActionResult> DeclineRequest(int id)
        {
            var current = await _userManager.GetUserAsync(HttpContext.User);
            var users = _dbContext.Users.Include(nameof(CustomIdentityUser.FriendRequests));
            var user = users.FirstOrDefault(u => u.Id == current.Id);
            var items = user?.FriendRequests.Where(r => r.ReceiverId == user.Id);

            var request = await _dbContext.FriendRequests.FirstOrDefaultAsync(r => r.Id == id);
            var senderId = request?.SenderId;

            var sender = await users.FirstOrDefaultAsync(u => u.Id == senderId);

            _dbContext.FriendRequests.Add(new FriendRequest
            {
                Content=$"${sender?.UserName} decline your friend request at {DateTime.Now.ToLongTimeString()}",
                SenderId=senderId,
                CustomIdentityUser=sender,
                Status="Notification",
                ReceiverId=request.ReceiverId,
            });

            _dbContext.FriendRequests.Remove(request);
            await _dbContext.SaveChangesAsync();



            return Ok(items);
        }

        public async Task<IActionResult> DeleteRequest(int requestId)
        {
            var item = await _dbContext.FriendRequests.FirstOrDefaultAsync(r => r.Id == requestId);
           if(item!=null)
            _dbContext.FriendRequests.Remove(item);
           await _dbContext.SaveChangesAsync();
            return Ok();
        }

        public async Task<IActionResult> AcceptRequest(string userId,int requestId)
        {
            var receiverUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var sender = await _userManager.GetUserAsync(HttpContext.User);

            if (receiverUser != null)
            {
                receiverUser.FriendRequests.Add(new FriendRequest
                {
                     Content=$"{sender.UserName} accepted friend request at ${DateTime.Now.ToLongDateString()}",
                     SenderId=sender.Id,
                     CustomIdentityUser=sender,
                     Status="Notification",
                     ReceiverId=receiverUser.Id
                });

                var receiverFriend = new Friend
                {
                    OwnId = receiverUser.Id,
                    YourFriendId = sender.Id
                };


                var senderFriend = new Friend
                {
                    OwnId = sender.Id,
                    YourFriendId = receiverUser.Id
                };

                _dbContext.Friends.Add(senderFriend);
                _dbContext.Friends.Add(receiverFriend);

                var request = await _dbContext.FriendRequests.FirstOrDefaultAsync(r=>r.Id==requestId);
                _dbContext.FriendRequests.Remove(request);

                await _userManager.UpdateAsync(receiverUser);
                await _userManager.UpdateAsync(sender);

                await _dbContext.SaveChangesAsync();

            }
            return Ok();
        }

        public IActionResult Unfollow(string id)
        {
            var f = _dbContext.Friends.ToList();
            var results = _dbContext.Friends.Where(f => f.OwnId == id || f.YourFriendId == id).ToArray();
            _dbContext.Friends.RemoveRange(results);
            _dbContext.SaveChanges();
            var a  = _dbContext.Friends.ToList();
            return Ok();
        }    
    }
}