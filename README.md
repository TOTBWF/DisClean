# DisClean
Bot for deleting all messages from a given user in a discord server

# Compiling:
msbuild /t:restore && msbuild

# Usage:
scrubber [author] [channel] [token]
Author: the author of the messages that you would like to delete
Channel: The channel id that you would like to clean. Can be found by turning on developer mode
Token: The access token of your bot. This is given when the bot user is created
