using Arc3.Core.Attributes;
using Arc3.Core.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ARC3Test.Core.Attributes;

[TestClass]
public class KaraokePreconditionAttributeTest
{
  
  [TestMethod]
  [
    // This case, the user is in a voice channel where the admin user is different but the channel isnt locked.
    // They should be able to run the command.
    DataRow(
      "000000000000000001", // Voice channel ID
      "000000000000000001", // Channel that the user sent the command in
      false, // Is the voice channel locked?
      "000000000000000007", // Admin user ID,
      false, // Does the user have guild permissions?
      "000000000000000009", // Invoker ID
      true // Is the command successful?
    ),
      
    // This case, the user is in a voice channel where the admin user is different but the channel IS locked.
    // They should NOT be able to run the command.
    DataRow(
      "000000000000000001",
      "000000000000000001", 
      true, 
      "000000000000000007", 
      false,
      "000000000000000009",
      false
    ),
    
    // This case, the user is in a voice channel where the admin user is SAME but the channel IS locked.
    // They should be able to run the command.
    DataRow(
      "000000000000000001",
      "000000000000000001", 
      true, 
      "000000000000000009", 
      false,
      "000000000000000009",
      true
    ),
    
    // This case, the user is in a voice channel but the command channel is different.
    // They should NOT be able to run the command.
    DataRow(
      "000000000000000001",
      "000000000000000011", 
      true, 
      "000000000000000007",
      false,
      "000000000000000009",
      false
    ),
    
    // This case, the user is in a voice channel, is not the admin but has guild perms.
    // They should be able to run the command.
    DataRow(
      "000000000000000001",
      "000000000000000001", 
      true, 
      "000000000000000007",
      true,
      "000000000000000009",
      true
    ),
    
    // This case, the user is not  a voice channel,
    // They should NOT be able to run the command.
    DataRow(
      null,
      "000000000000000001", 
      true, 
      "000000000000000007",
      true,
      "000000000000000009",
      false
    ),
    
  ]
  public void PreconditionTest(
    string? voiceChannelId, string commandChannelId, bool lockStatus,
    string adminUserId, bool userGuildPerms, string userId, bool expectedSuccess)
  {
    
    // Vars
    ulong? testCaseVoiceChannelId = voiceChannelId != null? ulong.Parse(voiceChannelId) : null;
    var testCaseCommandChannelId = commandChannelId;
    var testCaseChannelLockStatus = lockStatus;
    var testCaseAdminUserId = ulong.Parse(adminUserId);
    var testCaseUserId = ulong.Parse(userId);
    var testCaseGuildPerms = userGuildPerms ? GuildPermissions.All : GuildPermissions.None;
    // ARRANGE
    var fakeVoiceChannel = new Mock<IVoiceChannel>();
    var fakeMessageChannel = new Mock<IMessageChannel>();
    var fakeUser = new Mock<IGuildUser>();
    var fakeGuild = new Mock<IGuild>();
    var userTask = Task.FromResult(fakeUser.Object);
    var mockInteractionContext = new Mock<IInteractionContext>();
    var mockServiceProvider = new Mock<IServiceProvider>();
    var mockKaraokeService = new KaraokeService(new Mock<DiscordSocketClient>().Object, null, null);
    var fakeChannelCache = new DefaultDict<ulong, ChannelStatus>(new ChannelStatus());
    
    // Setup fake discord context
    
    if (testCaseVoiceChannelId.HasValue)
      fakeVoiceChannel.SetupGet( x => x.Id )
        .Returns(testCaseVoiceChannelId.Value);
    
    fakeMessageChannel.SetupGet(x => x.Id)
      .Returns(ulong.Parse(testCaseCommandChannelId));
    fakeUser.SetupGet( x => x.VoiceChannel)
      .Returns((testCaseVoiceChannelId != null? fakeVoiceChannel.Object : null)!);
    fakeUser.SetupGet( x => x.Id)
      .Returns(testCaseUserId);
    fakeUser.SetupGet(x => x.GuildPermissions)
      .Returns(testCaseGuildPerms);
    fakeGuild.Setup( x => x.GetUserAsync(It.IsAny<ulong>(), CacheMode.AllowDownload, null))
      .Returns(userTask);
    
    mockInteractionContext.SetupGet( x => x.Guild).Returns(fakeGuild.Object);
    mockInteractionContext.SetupGet( x => x.Channel).Returns(fakeMessageChannel.Object);
    mockInteractionContext.SetupGet( x => x.User).Returns(fakeUser.Object);
    mockInteractionContext.SetupGet(x => x.Interaction).Returns(new Mock<IDiscordInteraction>().Object);
    
    // Setup mock services
    if (testCaseVoiceChannelId != null)
    {
      fakeChannelCache[testCaseVoiceChannelId.Value].AdminSnowflake = testCaseAdminUserId;
      fakeChannelCache[testCaseVoiceChannelId.Value].Locked = testCaseChannelLockStatus;
    }

    mockKaraokeService.ChannelCache = fakeChannelCache;
    
    mockServiceProvider.Setup( x => x.GetService(It.IsAny<Type>()))
      .Returns(mockKaraokeService);

    var karaokePrecondition = new KaraokePreconditionAttribute();
    
    // Assert 
    
    var result = karaokePrecondition.CheckRequirementsAsync(
      mockInteractionContext.Object,
      new Mock<ICommandInfo>().Object,
      mockServiceProvider.Object
    ).Result;
    
    Assert.AreEqual(result.IsSuccess, expectedSuccess);
  }

}