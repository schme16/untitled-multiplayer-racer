var WebRTC = {
	wrtcInitialize: () => {
		console.log(1111)
	},
	_wrtcInitialize: () => {
		console.log(2222)
	}
}



mergeInto(LibraryManager.library, WebRTC);
