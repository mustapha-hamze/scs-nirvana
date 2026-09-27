// Slider feature JS: shared by Index, List, Create, and the slider-item form/list (moved from
// Areas/BackOffice/Views/Slider/__SliderJSFunctions.cshtml).

    function createSliderItem() {
        const fileInput = $('#sliderItemImage');
        const fileName = fileInput.val();
        const file = fileInput.prop('files')[0];

        if (file == undefined) {
            messageBox("Error!", "Please select an image for slide.", "error");
            return;
        }

        var arrFile = file.name.split('.');
        if (arrFile[arrFile.length - 1] != 'jpg') {
            messageBox("Error!", "The image of slide must be JPG.", "error");
            return;
        }

        setLoadingForBtn("__btnCreateSliderItem__");
        var frmCreateSliderItemForm = $("#__frmCreateSliderItemForm__");
        $.ajax({
            type: "POST",
            url: "/BackOffice/Slider/CreateItem",
            data: frmCreateSliderItemForm.serialize()
        }).done(function (data) {
            const _data = data.split('|');
            uploadSliderItemImage(_data[0], _data[1]);
        });
    }

    function uploadSliderItemImage(sliderId, imageFileName) {
        const fileInput = $('#sliderItemImage');
        const file = fileInput.prop('files')[0];
        const formData = new FormData();
        formData.append('file', file);
        $.ajax({
            url: `/BackOffice/Slider/UploadSliderItemImage?sliderId=${sliderId}&imageFileName=${imageFileName}`,
            method: 'POST',
            data: formData,
            processData: false,
            contentType: false
        }).done(function () {
            if ($("#__sliderItem_FRM_ImageFileName").val() == "") {
                removeLoadingForBtn("__btnCreateSliderItem__");
            } else {
                removeLoadingForBtn("__btnUpdateSliderItem__");
            }
            sliderItems($("#__sliderItem_FRM_SliderId").val());
        });
    }

    function getSliderForm() {
        $.ajax({
            url: '/BackOffice/Slider/Create',
            type: "GET"
        }).done(function (data) {
            $("#sliderFormPlaceHolder").html(data);
        });
    }

    function getSliderList() {
        $.ajax({
            url: '/BackOffice/Slider/List',
            type: "GET"
        }).done(function (data) {
            $("#findSlidersResultBody").html(data);
            // An empty list renders an empty state instead of the table.
            if ($("#datatable-buttons").length === 0) return;
            $("#datatable-buttons").DataTable({
                lengthChange: !1,
                pageLength: 25,
                buttons: ["copy", "print"],
                order: [[1, "asc"]],
                // Id stays visible: Responsive only makes the first cell keyboard-focusable, so
                // it must carry the row-details toggle. Title next, then Actions.
                columnDefs: [
                    { orderable: false, targets: -1 },
                    { responsivePriority: 1, targets: 0 },
                    { responsivePriority: 2, targets: 1 },
                    { responsivePriority: 3, targets: -1 }
                ],
                language: {
                    zeroRecords: "No sliders match your search. Try a shorter or different term.",
                    paginate: {
                        previous: "<i class='mdi mdi-chevron-left'>",
                        next: "<i class='mdi mdi-chevron-right'>"
                    }
                },
                drawCallback: function () {
                    $(".dataTables_paginate > .pagination").addClass("pagination-rounded")
                }
            });
        });
    }

    function sliderItems(sliderId) {
        $.ajax({
            url: '/BackOffice/Slider/SliderItems',
            type: "GET"
        }).done(function (data) {
            $("#sliderItemModalBody").html(data);
            getSliderItemsForm(sliderId);
            getSliderItemsList(sliderId);
        });
    }

    function getSliderItemsForm(sliderId) {
        $.ajax({
            url: `/BackOffice/Slider/GetSliderItemForm/${sliderId}/0`,
            type: "GET"
        }).done(function (data) {
            $("#__sliderItemFormPlaceHolder__").html(data);
        });
    }

    function getSliderItemsList(sliderId) {
        $.ajax({
            url: `/BackOffice/Slider/GetSliderItemList/${sliderId}`,
            type: "GET"
        }).done(function (data) {
            $("#__sliderItemsListPlaceHolder__").html(data);
        });
    }

    function createSlider() {
        setLoadingForBtn("__btnCreateSlider__");
        var frmCreateSliderForm = $("#__frmCreateSliderForm__");
        $.ajax({
            type: "POST",
            url: "/BackOffice/Slider/Create",
            data: frmCreateSliderForm.serialize()
        }).done(function (data) {
            getSliderList();
            getSliderForm();
            removeLoadingForBtn("__btnCreateSlider__");
        });
    }

    function deactiveSliderItem(sliderItemId) {
        $.ajax({
            url: '/BackOffice/Slider/DeactiveItem?sliderItemId=' + sliderItemId,
            type: "POST"
        }).done(function (data) {
            sliderItems($("#__sliderItem_FRM_SliderId").val());
        });
    }

    function activeSliderItem(sliderItemId) {
        $.ajax({
            url: '/BackOffice/Slider/ActiveItem?sliderItemId=' + sliderItemId,
            type: "POST"
        }).done(function (data) {
            sliderItems($("#__sliderItem_FRM_SliderId").val());
        });
    }

    function deleteSliderItem(sliderItemId) {
        Swal.fire({
            title: "Delete this slide?",
            text: "The slide is removed from the slider. This can't be undone.",
            icon: "warning",
            showCancelButton: true,
            confirmButtonColor: "var(--scs-danger-solid)",
            confirmButtonText: "Delete slide",
            cancelButtonText: "Keep slide",
            focusCancel: true
        }).then((result) => {
            if (!result.isConfirmed) return;
            $.ajax({
                url: '/BackOffice/Slider/DeleteItem?sliderItemId=' + sliderItemId,
                type: "DELETE"
            }).done(function (data) {
                sliderItems($("#__sliderItem_FRM_SliderId").val());
            });
        });
    }

    // Shows the chosen file in the form's bounded preview before it is uploaded.
    function previewSliderItemImage(input) {
        const file = input.files[0];
        if (!file) return;
        const preview = $("#sliderItemImagePreview");
        preview.find("img").attr({ src: URL.createObjectURL(file), alt: "Preview of " + file.name });
        preview.find("figcaption").text(file.name + " (not saved yet)");
        preview.prop("hidden", false);
        $("#sliderItemImageEmpty").prop("hidden", true);
    }

    function editSliderItem(sliderItemId, sliderId) {
        $.ajax({
            url: `/BackOffice/Slider/GetSliderItemForm/${sliderId}/${sliderItemId}`,
            type: "GET"
        }).done(function (data) {
            $("#__sliderItemFormPlaceHolder__").html(data);
        });
    }

    function updateSliderItem() {
        const fileInput = $('#sliderItemImage');
        const fileName = fileInput.val();
        const file = fileInput.prop('files')[0];

        if (file != undefined) {
            var arrFile = file.name.split('.');
            if (arrFile[arrFile.length - 1] != 'jpg') {
                messageBox("Error!", "The image of silde must be JPG.", "error");
                return;
            }
        }

        setLoadingForBtn("__btnUpdateSliderItem__");
        var frmCreateSliderItemForm = $("#__frmCreateSliderItemForm__");
        $.ajax({
            type: "POST",
            url: "/BackOffice/Slider/UpdateItem",
            data: frmCreateSliderItemForm.serialize()
        }).done(function () {
            if (file != undefined) {
                uploadSliderItemImage($("#__sliderItem_FRM_SliderId").val(), $("#__sliderItem_FRM_ImageFileName").val());
            } else {
                removeLoadingForBtn("__btnUpdateSliderItem__");
            }
        });
    }
