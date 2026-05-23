$(document).ready(function() {
     var form = $('#inputValidation')[0];
     var pristine = new Pristine(form);    
     $('#submitBtn').on('click', function(e) {
       e.preventDefault();
       var valid = pristine.validate();
       if (!valid) {          
         console.log('Form has validation errors.');        
         return false; 
       }else{
         form.submit();
       }
     });
   });