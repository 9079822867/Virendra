function showPosition(position) {
    $('#latitude').val(position.coords.latitude);
    $('#longitude').val(position.coords.longitude);
}

function getLocation() {
    return new Promise((resolve, reject) => {
        if (navigator.permissions) {
            navigator.permissions.query({ name: 'geolocation' }).then(function(permissionStatus) {
                if (permissionStatus.state === 'granted' || permissionStatus.state === 'prompt') {
                    navigator.geolocation.getCurrentPosition(resolve, reject);
                } else {
                    reject({ code: 1 });  // PERMISSION_DENIED
                }
            }).catch(function(error) {
                console.error('Permission query error:', error);
                reject({ code: 1 });  // Fallback to permission denied error
            });
        } else if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(resolve, reject);
        } else {
            reject({ code: 3 });  // UNKNOWN_ERROR
        }
    });
}

function showError(error) {
    var msg = "";
    switch (error.code) {
        case 1:  // PERMISSION_DENIED
            msg = "Permission denied. For security purposes, you must allow location access.";
            break;
        case 2:  // POSITION_UNAVAILABLE
            msg = "Location information is unavailable. Please try again later.";
            break;
        case 3:  // TIMEOUT
            msg = "The request to get user location timed out. Please try again.";
            break;
        default:  // UNKNOWN_ERROR
            msg = "An unknown error occurred while fetching location. Please try again.";
            break;
    }

    $('#error').text(msg);

    // Disable login button if there's an error
    // $('#loginButton').prop('disabled', msg !== "");

    return false;
}

async function handleLocation() {
    try {
        const position = await getLocation();
        showPosition(position);
        // Continue with further actions after getting the location
        // For example, enable the login button or submit the form
        $('#loginButton').prop('disabled', false);
    } catch (error) {
        showError(error);
    }
}

// Call handleLocation when needed
handleLocation();
