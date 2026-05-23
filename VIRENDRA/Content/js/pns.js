document.addEventListener('DOMContentLoaded', function () {
    var currentYear = new Date().getFullYear();
    var yearElements = document.getElementsByClassName('currentYear');

    for (var i = 0; i < yearElements.length; i++) {
        yearElements[i].textContent = currentYear;
    }
});

// document.getElementById("mobileInput").addEventListener("input", function() {
//     if (this.value.length > 10) {
//         this.value = this.value.slice(0, 10); // Truncate to 10 characters
//     }
// });

function isNumberKey(evt) {
    var ASCIICode = (evt.which) ? evt.which : evt.keyCode 
    if (ASCIICode > 31 && (ASCIICode < 48 || ASCIICode > 57)) 
        return false; 
    return true; 
  } 

function checkDec(el){
    var ex = /^[0-9]+\.?[0-9]*$/;
        if(ex.test(el.value)==false){
            el.value = el.value.substring(0,el.value.length - 1);
        }
}

function confirmBox(url, msg = "You won't be able to revert this!"){
    Swal.fire({
        title: "Are you sure?",
        text: msg,
        icon: "warning",
        showCancelButton: true,
        confirmButtonColor: "#2ab57d",
        cancelButtonColor: "#fd625e",
        confirmButtonText: "Yes"
    }).then(function(result) {
        if (result.isConfirmed) {
        window.location.href = url  
        }
    });
}