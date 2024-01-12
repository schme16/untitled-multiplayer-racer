var WebRTCComms = {
	$WebRTCComms: {},
	unityInitialized: false,
	initWebRTCComms__postset: '_initWebRTCComms();',

	SyncPlayerPositionJS: function (x, y, z, rX, rY, rZ, rW) {
		//console.log('Syncing player position (javascript side)', x, y, z, rX, rY, rZ, rW)
		if (WebRTCComms.channelConnected) {
			WebRTCComms.channel.emit('player-sync', JSON.stringify({
				pos: {x, y, z},
				rot: {x: rX, y: rY, z: rZ, w: rW}
			}))
		}
	},

	SyncRoomVariablesJS: function (state, playersInputEnabled, playerFinished, countdownStarted, countdownFinished) {
		//console.log('Syncing some variables (javascript side)')
		if (WebRTCComms.channelConnected) {

			WebRTCComms.channel.emit('room-sync', JSON.stringify({
				state: UTF8ToString(state), playersInputEnabled: playersInputEnabled == 1,
				playerFinished: UTF8ToString(playerFinished),
				countdownStarted: parseInt(UTF8ToString((countdownStarted))),
				countdownFinished: parseInt(UTF8ToString((countdownFinished)))
			}))
		}
	},

	CreateRoomJS: function (privateGame) {
		dynCall_vi(WebRTCComms.connectingToServer, 1)

		WebRTCComms.ConnectToServerJS(function () {
			WebRTCComms.channel.emit('room-new', JSON.stringify({privateGame: privateGame == 1}))
		})
	},

	JoinRoomJS: function (roomID) {

		var code = UTF8ToString(roomID)

		dynCall_vi(WebRTCComms.connectingToServer, 1)

		WebRTCComms.ConnectToServerJS(function () {
			//console.log('Attempting to join room:', code)
			WebRTCComms.channel.emit('room-join', JSON.stringify({roomID: code}))
		})
	},

	DisconnectFromServerJS: function (callback) {
		if (WebRTCComms.channelConnected) {
			WebRTCComms.channel.close()
		}
	},

	initWebRTCComms: function (serverDisconnected, roomJoined, roomSync, playerJoined, playerSync, playerLeft, connectingToServer) {

		WebRTCComms.unityInitialized = true;

		WebRTCComms.serverDisconnected = serverDisconnected
		WebRTCComms.roomJoined = roomJoined
		WebRTCComms.roomSync = roomSync
		WebRTCComms.playerJoined = playerJoined
		WebRTCComms.playerSync = playerSync
		WebRTCComms.playerLeft = playerLeft
		WebRTCComms.connectingToServer = connectingToServer

		WebRTCComms.ConnectToServerJS = function (callback) {
			if (WebRTCComms.channelConnected) {
				WebRTCComms.channel.close()
			}

			WebRTCComms.channel = geckos({
				url: 'https://cube-run.shanegadsby.com',
				port: 443
				//port: 7777
			})

			WebRTCComms.channel.onConnect(error => {
				if (error) {
					WebRTCComms.channelConnected = false;
					dynCall_vi(WebRTCComms.serverDisconnected, 1)
				}
				else {
					WebRTCComms.channelConnected = true

					WebRTCComms.channel.on('room-joined', (data) => {
						let jsonData = JSON.parse(data)
						jsonData.serverTime = WebRTCComms.syncedServerTime
						
						let jsonString = JSON.stringify(jsonData)

						var len1 = lengthBytesUTF8(jsonString) + 1,
							strPtr1 = _malloc(len1)

						stringToUTF8(jsonString, strPtr1, len1)
						dynCall_vi(WebRTCComms.roomJoined, strPtr1)
					})

					WebRTCComms.channel.on('room-sync', (data) => {
						var len1 = lengthBytesUTF8(data) + 1,
							strPtr1 = _malloc(len1)

						stringToUTF8(data, strPtr1, len1)
						dynCall_vi(WebRTCComms.roomSync, strPtr1)
					})

					WebRTCComms.channel.on('player-joined', (data) => {
						//console.log('Player joined (JS)', data)

						var len1 = lengthBytesUTF8(data) + 1,
							strPtr1 = _malloc(len1)

						stringToUTF8(data, strPtr1, len1)
						dynCall_vi(WebRTCComms.playerJoined, strPtr1)
					})

					WebRTCComms.channel.on('player-sync', (data) => {
						//console.log('Player synced (JS)', data)

						var len1 = lengthBytesUTF8(data) + 1,
							strPtr1 = _malloc(len1)

						stringToUTF8(data, strPtr1, len1)
						dynCall_vi(WebRTCComms.playerSync, strPtr1)
					})

					WebRTCComms.channel.on('player-left', (data) => {
						//console.log('Player left (JS)', data)

						var len1 = lengthBytesUTF8(data) + 1,
							strPtr1 = _malloc(len1)

						stringToUTF8(data, strPtr1, len1)
						dynCall_vi(WebRTCComms.playerLeft, strPtr1)
					})

					WebRTCComms.channel.on('time-sync', (data) => {
						var jsonData = JSON.parse(data),

							// Get current timestamp in milliseconds
							nowTimeStamp = new Date().getTime(),

							// Parse server-client difference time and server timestamp from response
							serverClientRequestDiffTime = jsonData.diff,
							serverTimestamp = jsonData.serverTimestamp,

							// Calculate server-client difference time on response and response time
							serverClientResponseDiffTime = nowTimeStamp - serverTimestamp,
							responseTime = (serverClientRequestDiffTime - nowTimeStamp + jsonData.clientTimestamp - serverClientResponseDiffTime) / 2

						// Calculate the synced server time
						WebRTCComms.serverSecondDiff = (serverClientResponseDiffTime - responseTime)

						WebRTCComms.syncedServerTime = nowTimeStamp + WebRTCComms.serverSecondDiff

						// You may want to do something with syncedServerTime here. For this example, we'll just alert.
						console.log("Server sync time:", WebRTCComms.serverSecondDiff, WebRTCComms.syncedServerTime);

						/*var len1 = lengthBytesUTF8(data) + 1,
							strPtr1 = _malloc(len1)

						stringToUTF8(data, strPtr1, len1)
						dynCall_vi(WebRTCComms.playerLeft, strPtr1)*/
					})

					WebRTCComms.channel.onDisconnect(() => {
						WebRTCComms.channelConnected = false;
						dynCall_vi(WebRTCComms.serverDisconnected, 1)
					})

					//Ping for server time diff
					WebRTCComms.channel.emit('time-sync', JSON.stringify({timestamp: new Date().getTime()}))

					if (typeof callback == 'function') {
						callback()
					}
				}
			})
		}

	}

};

autoAddDeps(WebRTCComms, '$WebRTCComms');
mergeInto(LibraryManager.library, WebRTCComms);
